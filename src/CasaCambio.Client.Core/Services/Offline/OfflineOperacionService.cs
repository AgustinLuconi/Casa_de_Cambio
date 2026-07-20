using System;
using System.Threading.Tasks;
using CasaCambio.Shared.Enums;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using SistemaCambio.ApiClient;
using SistemaCambio.LocalDb;

namespace SistemaCambio.Services.Offline;

public class OfflineOperacionService : IOfflineOperacionService
{
    private readonly ICasaCambioApiClient _apiClient;
    private readonly IDbContextFactory<LocalDbContext> _localDbFactory;
    private readonly IConnectivityChecker _connectivity;

    public event Action<string>? OnOperacionGuardadaOffline;

    public OfflineOperacionService(
        ICasaCambioApiClient apiClient,
        IDbContextFactory<LocalDbContext> localDbFactory,
        IConnectivityChecker connectivity)
    {
        _apiClient = apiClient;
        _localDbFactory = localDbFactory;
        _connectivity = connectivity;
    }

    public async Task<OfflineOperacionResult> GuardarCompraAsync(CrearOperacionRequest request)
        => await GuardarOperacionAsync("Compra", request);

    public async Task<OfflineOperacionResult> GuardarVentaAsync(CrearOperacionRequest request)
        => await GuardarOperacionAsync("Venta", request);

    public async Task<OfflineArbitrajeResult> GuardarArbitrajeAsync(CrearArbitrajeRequest request)
    {
        var key = Guid.NewGuid().ToString();
        request.IdempotencyKey = key;

        var local = new LocalOperacion
        {
            Id = key,
            TipoOperacion = "Arbitraje",
            CuentaOrigenId = request.CuentaPesosId,
            CuentaDestinoId = request.CuentaAcreditaCompraId,
            MonedaOrigen = "ARS",
            MonedaDestino = request.MonedaCompra,
            MontoOrigen = request.PesosCompra,
            MontoDestino = request.MontoExtranjeroCompra,
            CotizacionAplicada = request.CotizacionCompra,
            Observaciones = request.Observaciones,
            CuentaDebitaVentaId = request.CuentaDebitaVentaId,
            MonedaVenta = request.MonedaVenta,
            MontoExtranjeroVenta = request.MontoExtranjeroVenta,
            CotizacionVenta = request.CotizacionVenta,
            TipoOperacionArbitraje = request.TipoOperacion
        };

        if (await _connectivity.CheckAsync())
        {
            try
            {
                var response = await _apiClient.CrearArbitrajeAsync(request);
                return new OfflineArbitrajeResult
                {
                    Exitoso = response.Exitoso,
                    Mensaje = response.Mensaje,
                    OperacionIdCompra = response.OperacionIdCompra,
                    OperacionIdVenta = response.OperacionIdVenta,
                    IsOffline = false
                };
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                // El servidor respondió (validación, error de negocio, etc.) — no es un problema de conectividad.
                return new OfflineArbitrajeResult { Exitoso = false, Mensaje = ex.Message, IsOffline = false };
            }
            catch (Exception ex)
            {
                return await GuardarLocalArbitrajeAsync(local, ex.Message);
            }
        }

        return await GuardarLocalArbitrajeAsync(local);
    }

    public async Task<OfflineOperacionResult> GuardarCreditoDebitoAsync(CrearCreditoDebitoRequest request)
    {
        var key = Guid.NewGuid().ToString();
        request.IdempotencyKey = key;

        var local = new LocalOperacion
        {
            Id = key,
            TipoOperacion = "CreditoDebito",
            CuentaOrigenId = request.CuentaDebitoId,
            CuentaDestinoId = request.CuentaCreditoId,
            MonedaOrigen = request.MonedaDebito,
            MonedaDestino = request.MonedaCredito,
            MontoOrigen = request.MontoDebito,
            MontoDestino = request.MontoCredito,
            CotizacionAplicada = request.Cotizacion,
            Observaciones = request.Observaciones
        };

        if (await _connectivity.CheckAsync())
        {
            try
            {
                var response = await _apiClient.CrearCreditoDebitoAsync(request);
                return new OfflineOperacionResult { Exitoso = response.Exitoso, Mensaje = response.Mensaje, OperacionId = response.OperacionId, IsOffline = false };
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                // El servidor respondió (validación, error de negocio, etc.) — no es un problema de conectividad.
                return new OfflineOperacionResult { Exitoso = false, Mensaje = ex.Message, IsOffline = false };
            }
            catch (Exception ex)
            {
                return await GuardarLocalAsync(local, ex.Message);
            }
        }

        return await GuardarLocalAsync(local);
    }

    private async Task<OfflineOperacionResult> GuardarOperacionAsync(string tipo, CrearOperacionRequest request)
    {
        var key = Guid.NewGuid().ToString();
        request.IdempotencyKey = key;

        var local = CrearLocalOperacion(tipo, request);
        local.Id = key;

        if (await _connectivity.CheckAsync())
        {
            try
            {
                var response = tipo == "Compra"
                    ? await _apiClient.CrearCompraAsync(request)
                    : await _apiClient.CrearVentaAsync(request);
                return new OfflineOperacionResult { Exitoso = response.Exitoso, Mensaje = response.Mensaje, OperacionId = response.OperacionId, IsOffline = false };
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                // El servidor respondió (validación, error de negocio, etc.) — no es un problema de conectividad.
                return new OfflineOperacionResult { Exitoso = false, Mensaje = ex.Message, IsOffline = false };
            }
            catch (Exception ex)
            {
                return await GuardarLocalAsync(local, ex.Message);
            }
        }

        return await GuardarLocalAsync(local);
    }

    private static LocalOperacion CrearLocalOperacion(string tipo, CrearOperacionRequest request) => new()
    {
        TipoOperacion = tipo,
        CuentaOrigenId = request.CuentaOrigenId,
        CuentaDestinoId = request.CuentaDestinoId,
        MonedaOrigen = request.MonedaOrigen,
        MonedaDestino = request.MonedaDestino,
        MontoOrigen = request.MontoOrigen,
        MontoDestino = request.MontoDestino,
        CotizacionAplicada = request.Cotizacion,
        Observaciones = request.Observaciones
    };

    private async Task<OfflineOperacionResult> GuardarLocalAsync(LocalOperacion operacion, string? motivo = null)
    {
        using var db = _localDbFactory.CreateDbContext();
        db.OperacionesPendientes.Add(operacion);
        await db.SaveChangesAsync();
        OnOperacionGuardadaOffline?.Invoke(operacion.Id);
        return new OfflineOperacionResult
        {
            Exitoso = true,
            Mensaje = motivo != null
                ? $"Guardada localmente (error de red: {motivo}). Se sincronizara automaticamente."
                : "Guardada localmente. Se sincronizara cuando haya conexion.",
            LocalId = operacion.Id,
            IsOffline = true
        };
    }

    private async Task<OfflineArbitrajeResult> GuardarLocalArbitrajeAsync(LocalOperacion operacion, string? motivo = null)
    {
        using var db = _localDbFactory.CreateDbContext();
        db.OperacionesPendientes.Add(operacion);
        await db.SaveChangesAsync();
        OnOperacionGuardadaOffline?.Invoke(operacion.Id);
        return new OfflineArbitrajeResult
        {
            Exitoso = true,
            Mensaje = motivo != null
                ? $"Guardada localmente (error de red: {motivo}). Se sincronizara automaticamente."
                : "Guardada localmente. Se sincronizara cuando haya conexion.",
            LocalId = operacion.Id,
            IsOffline = true
        };
    }

    public async Task<int> ObtenerPendientesCountAsync()
    {
        using var db = _localDbFactory.CreateDbContext();
        return await db.OperacionesPendientes
            .CountAsync(o => o.EstadoSync == EstadoSincronizacion.Pendiente || o.EstadoSync == EstadoSincronizacion.Error);
    }
}

public class OfflineOperacionResult
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionId { get; set; }
    public string? LocalId { get; set; }
    public bool IsOffline { get; set; }
}

public class OfflineArbitrajeResult
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionIdCompra { get; set; }
    public int? OperacionIdVenta { get; set; }
    public string? LocalId { get; set; }
    public bool IsOffline { get; set; }
}
