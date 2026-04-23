using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.Responses;

namespace SistemaCambio.ApiClient;

public interface ICasaCambioApiClient
{
    // Auth
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken);
    Task<bool> HealthCheckAsync();
    Task<RegisterResponse> RegistrarUsuarioAsync(RegisterRequest request);
    Task<bool> RecuperarPasswordAsync(RecuperarPasswordRequest request);
    Task<UsuarioPerfilDto?> ObtenerPerfilAsync();
    Task<bool> ActualizarPerfilAsync(ActualizarPerfilRequest request);
    Task<bool> CambiarPasswordAsync(CambiarPasswordRequest request);
    Task<RegisterResponse> ReenviarConfirmacionAsync();
    Task<RegisterResponse> ResetearPasswordAsync(ResetearPasswordRequest request);

    // Operaciones
    Task<OperacionResponse> CrearCompraAsync(CrearOperacionRequest request);
    Task<OperacionResponse> CrearVentaAsync(CrearOperacionRequest request);
    Task<OperacionResponse> CrearCreditoDebitoAsync(CrearCreditoDebitoRequest request);
    Task<OperacionResponse> CrearInterbancarioAsync(CrearInterbancarioRequest request);
    Task<PaginatedResponse<OperacionDto>> ObtenerOperacionesAsync(DateTime? desde = null, DateTime? hasta = null, string? tipo = null, int page = 1, int pageSize = 50);
    Task<OperacionDto?> ObtenerOperacionAsync(int id);

    // Cuentas
    Task<List<CuentaDto>> ObtenerCuentasAsync();
    Task<CuentaDto> CrearCuentaAsync(CrearCuentaRequest request);
    Task<CuentaDto> ActualizarCuentaAsync(int cuentaId, CrearCuentaRequest request);
    Task EliminarCuentaAsync(int cuentaId);
    Task<PaginatedResponse<MovimientoDto>> ObtenerMovimientosCuentaAsync(int cuentaId, DateTime? desde = null, DateTime? hasta = null, int page = 1, int pageSize = 200);
    Task<List<SaldoCuentaDto>> ObtenerSaldosCuentaAsync(int cuentaId);
    Task<bool> ObtenerEstadoDiaCerradoAsync();

    // Monedas
    Task<List<MonedaDto>> ObtenerMonedasAsync();
    Task<MonedaDto> CrearMonedaAsync(CrearMonedaRequest request);
    Task<MonedaDto> ActualizarMonedaAsync(int id, ActualizarMonedaRequest request);
    Task EliminarMonedaAsync(int id);

    // Cotizaciones
    Task<List<CotizacionDto>> ObtenerCotizacionesHoyAsync();
    Task GuardarCotizacionAsync(CrearCotizacionRequest request);

    // Clientes
    Task<List<ClienteDto>> ObtenerClientesAsync();

    // Arqueo
    Task<ArqueoDto> RealizarArqueoAsync(CrearArqueoRequest request);

    // Cierre de Caja
    Task<CierreCajaDto?> ObtenerCierreHoyAsync();
    Task<CierreCajaDto> GenerarCierreAsync(string observaciones = "");
    Task<CierreCajaDto> CerrarDefinitivoAsync(int id);

    // Configuración
    Task<string?> ObtenerConfiguracionAsync(string clave);
    Task<bool> ActualizarConfiguracionAsync(string clave, string valor);

    // PPP
    Task<decimal> ObtenerPPPAsync(string moneda);
    Task<PPPValidacionDto> ValidarVentaPPPAsync(string moneda, decimal cotizacion);

    // Dashboard
    Task<DashboardDto> ObtenerDashboardAsync();

    // Sync
    Task<SyncPushResponse> SyncPushAsync(SyncPushRequest request);
    Task<SyncPullResponse> SyncPullAsync();

    // Events
    event Action? OnSessionExpired;
}

public class PPPValidacionDto
{
    public string Moneda { get; set; } = "";
    public decimal PPP { get; set; }
    public decimal CotizacionVenta { get; set; }
    public decimal Ganancia { get; set; }
    public bool EsRentable { get; set; }
}

public class SyncPullResponse
{
    public List<CuentaDto> Cuentas { get; set; } = new();
    public List<MonedaDto> Monedas { get; set; } = new();
    public List<CotizacionDto> Cotizaciones { get; set; } = new();
}
