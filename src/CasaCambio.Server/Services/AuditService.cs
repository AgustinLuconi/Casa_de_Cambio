using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Server.Models;
using System.Text.Json;

namespace CasaCambio.Server.Services;

public class AuditService : IAuditService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public AuditService(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    public void Registrar(string accion, string entidad, int entidadId, object? datosAnteriores = null, object? datosNuevos = null, string? usuario = null)
    {
        try
        {
            using var db = _contextFactory.CreateDbContext();
            var log = new AuditLog
            {
                Fecha = DateTime.Now,
                Accion = accion,
                Entidad = entidad,
                EntidadId = entidadId,
                UsuarioNombre = usuario ?? "Admin",
                ValoresAnteriores = datosAnteriores != null ? JsonSerializer.Serialize(datosAnteriores) : null,
                ValoresNuevos = datosNuevos != null ? JsonSerializer.Serialize(datosNuevos) : null
            };
            db.AuditLogs.Add(log);
            db.SaveChanges();
        }
        catch { }
    }

    public void RegistrarCambioCotizacion(int monedaId, decimal cotizacionAnterior, decimal cotizacionNueva)
    {
        Registrar("UPDATE", "CotizacionDiaria", monedaId, datosAnteriores: new { cotizacion = cotizacionAnterior }, datosNuevos: new { cotizacion = cotizacionNueva });
    }

    public void RegistrarEliminacion(string entidad, int entidadId, object datosEliminados)
    {
        Registrar("DELETE", entidad, entidadId, datosAnteriores: datosEliminados);
    }
}
