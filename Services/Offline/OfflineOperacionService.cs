using System;
using System.Threading.Tasks;
using CasaCambio.Shared.Enums;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using SistemaCambio.ApiClient;
using SistemaCambio.LocalDb;

namespace SistemaCambio.Services.Offline;

public class OfflineOperacionService
{
    private readonly ICasaCambioApiClient _apiClient;
    private readonly IDbContextFactory<LocalDbContext> _localDbFactory;
    private readonly ConnectivityChecker _connectivity;

    public event Action<string>? OnOperacionGuardadaOffline;

    public OfflineOperacionService(
        ICasaCambioApiClient apiClient,
        IDbContextFactory<LocalDbContext> localDbFactory,
        ConnectivityChecker connectivity)
    {
        _apiClient = apiClient;
        _localDbFactory = localDbFactory;
        _connectivity = connectivity;
    }

    public async Task<OfflineOperacionResult> GuardarCompraAsync(CrearOperacionRequest request)
        => await GuardarOperacionAsync("Compra", request);

    public async Task<OfflineOperacionResult> GuardarVentaAsync(CrearOperacionRequest request)
        => await GuardarOperacionAsync("Venta", request);

    public async Task<OfflineOperacionResult> GuardarCreditoDebitoAsync(CrearCreditoDebitoRequest request)
    {
        if (await _connectivity.CheckAsync())
        {
            try
            {
                var response = await _apiClient.CrearCreditoDebitoAsync(request);
                return new OfflineOperacionResult { Exitoso = response.Exitoso, Mensaje = response.Mensaje, OperacionId = response.OperacionId, IsOffline = false };
            }
            catch (Exception ex)
            {
                // Fall through to offline
                return await GuardarLocalAsync(new LocalOperacion
                {
                    TipoOperacion = "CreditoDebito",
                    CuentaOrigenId = request.CuentaDebitoId,
                    CuentaDestinoId = request.CuentaCreditoId,
                    MonedaOrigen = request.MonedaDebito,
                    MonedaDestino = request.MonedaCredito,
                    MontoOrigen = request.MontoDebito,
                    MontoDestino = request.MontoCredito,
                    CotizacionAplicada = request.Cotizacion,
                    ClienteId = request.ClienteId,
                    Observaciones = request.Observaciones
                }, ex.Message);
            }
        }

        return await GuardarLocalAsync(new LocalOperacion
        {
            TipoOperacion = "CreditoDebito",
            CuentaOrigenId = request.CuentaDebitoId,
            CuentaDestinoId = request.CuentaCreditoId,
            MonedaOrigen = request.MonedaDebito,
            MonedaDestino = request.MonedaCredito,
            MontoOrigen = request.MontoDebito,
            MontoDestino = request.MontoCredito,
            CotizacionAplicada = request.Cotizacion,
            ClienteId = request.ClienteId,
            Observaciones = request.Observaciones
        });
    }

    private async Task<OfflineOperacionResult> GuardarOperacionAsync(string tipo, CrearOperacionRequest request)
    {
        if (await _connectivity.CheckAsync())
        {
            try
            {
                var response = tipo == "Compra"
                    ? await _apiClient.CrearCompraAsync(request)
                    : await _apiClient.CrearVentaAsync(request);
                return new OfflineOperacionResult { Exitoso = response.Exitoso, Mensaje = response.Mensaje, OperacionId = response.OperacionId, IsOffline = false };
            }
            catch (Exception ex)
            {
                return await GuardarLocalAsync(CrearLocalOperacion(tipo, request), ex.Message);
            }
        }

        return await GuardarLocalAsync(CrearLocalOperacion(tipo, request));
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
        ClienteId = request.ClienteId,
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
