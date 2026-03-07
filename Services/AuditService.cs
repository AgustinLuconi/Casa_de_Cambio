using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using System;
using System.Text.Json;

namespace SistemaCambio.Services
{
    public class AuditService : IAuditService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public AuditService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Registra una acción en el log de auditoría.
        /// </summary>
        public void Registrar(
            string accion, 
            string entidad, 
            int entidadId,
            object? datosAnteriores = null, 
            object? datosNuevos = null,
            string? usuario = null)
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
                    ValoresAnteriores = datosAnteriores != null
                        ? JsonSerializer.Serialize(datosAnteriores)
                        : null,
                    ValoresNuevos = datosNuevos != null
                        ? JsonSerializer.Serialize(datosNuevos)
                        : null
                };

                db.AuditLogs.Add(log);
                db.SaveChanges();
            }
            catch
            {
                // Fallar silenciosamente para no interrumpir operaciones principales
            }
        }

        /// <summary>
        /// Registra una actualización de cotización.
        /// </summary>
        public void RegistrarCambioCotizacion(int monedaId, decimal cotizacionAnterior, decimal cotizacionNueva)
        {
            Registrar("UPDATE", "CotizacionDiaria", monedaId,
                datosAnteriores: new { cotizacion = cotizacionAnterior },
                datosNuevos: new { cotizacion = cotizacionNueva });
        }

        /// <summary>
        /// Registra eliminación de una operación.
        /// </summary>
        public void RegistrarEliminacion(string entidad, int entidadId, object datosEliminados)
        {
            Registrar("DELETE", entidad, entidadId, datosAnteriores: datosEliminados);
        }
    }
}
