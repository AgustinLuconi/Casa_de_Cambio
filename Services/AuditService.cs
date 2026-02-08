using SistemaCambio.Models;
using System;
using System.Text.Json;

namespace SistemaCambio.Services
{
    public static class AuditService
    {
        /// <summary>
        /// Registra una acción en el log de auditoría.
        /// </summary>
        public static void Registrar(
            string accion, 
            string entidad, 
            int entidadId,
            object? datosAnteriores = null, 
            object? datosNuevos = null,
            string? usuario = null)
        {
            try
            {
                using var db = new AppDbContext();

                var log = new AuditLog
                {
                    Fecha = DateTime.Now,
                    Accion = accion,
                    Entidad = entidad,
                    EntidadId = entidadId,
                    UsuarioNombre = usuario ?? "Admin", // TODO: Integrar con sistema de usuarios
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
                // En producción, loguear a archivo
            }
        }

        /// <summary>
        /// Registra una actualización de cotización.
        /// </summary>
        public static void RegistrarCambioCotizacion(int monedaId, decimal cotizacionAnterior, decimal cotizacionNueva)
        {
            Registrar("UPDATE", "CotizacionDiaria", monedaId,
                datosAnteriores: new { cotizacion = cotizacionAnterior },
                datosNuevos: new { cotizacion = cotizacionNueva });
        }

        /// <summary>
        /// Registra eliminación de una operación.
        /// </summary>
        public static void RegistrarEliminacion(string entidad, int entidadId, object datosEliminados)
        {
            Registrar("DELETE", entidad, entidadId, datosAnteriores: datosEliminados);
        }
    }
}
