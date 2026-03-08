namespace CasaCambio.Server.Services;

public interface IAuditService
{
    void Registrar(string accion, string entidad, int entidadId, object? datosAnteriores = null, object? datosNuevos = null, string? usuario = null);
    void RegistrarCambioCotizacion(int monedaId, decimal cotizacionAnterior, decimal cotizacionNueva);
    void RegistrarEliminacion(string entidad, int entidadId, object datosEliminados);
}
