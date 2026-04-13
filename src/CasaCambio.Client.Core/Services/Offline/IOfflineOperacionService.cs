using CasaCambio.Shared.Requests;

namespace SistemaCambio.Services.Offline;

public interface IOfflineOperacionService
{
    Task<OfflineOperacionResult> GuardarCompraAsync(CrearOperacionRequest request);
    Task<OfflineOperacionResult> GuardarVentaAsync(CrearOperacionRequest request);
    Task<OfflineOperacionResult> GuardarCreditoDebitoAsync(CrearCreditoDebitoRequest request);
    Task<int> ObtenerPendientesCountAsync();
    event Action<string>? OnOperacionGuardadaOffline;
}
