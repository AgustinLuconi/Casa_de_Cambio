using SistemaCambio.Models;
using System;
using System.Linq;

namespace SistemaCambio.Services
{
    public class OperacionResult
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; } = "";
        public int? OperacionId { get; set; }

        public static OperacionResult Success(int id) => new() { Exitoso = true, OperacionId = id };
        public static OperacionResult Error(string msg) => new() { Exitoso = false, Mensaje = msg };
    }

    public static class OperacionService
    {
        /// <summary>
        /// Guarda una operación de cambio con transacción atómica y validación de saldos.
        /// </summary>
        public static OperacionResult GuardarOperacion(
            string tipo,
            int cuentaOrigenId,
            int cuentaDestinoId,
            decimal montoOrigen,
            decimal montoDestino,
            decimal cotizacion,
            int? clienteId = null,
            string observaciones = "")
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId);
                var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);

                if (cuentaOrigen == null)
                    return OperacionResult.Error("Cuenta origen no encontrada");
                if (cuentaDestino == null)
                    return OperacionResult.Error("Cuenta destino no encontrada");

                // VALIDACIÓN CRÍTICA: Saldo no puede quedar negativo
                if (cuentaOrigen.Saldo < montoOrigen)
                    return OperacionResult.Error(
                        $"Saldo insuficiente en '{cuentaOrigen.Nombre}'. " +
                        $"Disponible: {cuentaOrigen.Saldo:N2}, Requerido: {montoOrigen:N2}");

                // Crear operación principal
                var operacion = new Operacion
                {
                    Fecha = DateTime.UtcNow,
                    TipoOperacion = tipo,
                    ClienteId = clienteId,
                    MontoTotalOrigen = montoOrigen,
                    MontoTotalDestino = montoDestino,
                    CotizacionAplicada = cotizacion,
                    Observaciones = observaciones
                };
                db.Operaciones.Add(operacion);

                // Movimiento DÉBITO (sale dinero)
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaOrigenId,
                    Monto = -montoOrigen,
                    Fecha = DateTime.UtcNow
                });

                // Movimiento CRÉDITO (entra dinero)
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaDestinoId,
                    Monto = montoDestino,
                    Fecha = DateTime.UtcNow
                });

                // Actualizar saldos de las cuentas
                cuentaOrigen.Saldo -= montoOrigen;
                cuentaDestino.Saldo += montoDestino;

                // Guardar todo en una transacción atómica
                db.SaveChanges();
                transaction.Commit();

                // Registrar en Audit Log (fuera de la transacción para no afectar la operación)
                try { AuditService.Registrar("CREATE", "Operacion", operacion.Id,
                    datosNuevos: new { tipo, montoOrigen, montoDestino, cotizacion }); } catch { }

                return OperacionResult.Success(operacion.Id);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                return OperacionResult.Error($"Error al guardar operación: {innerMsg}");
            }
        }

        /// <summary>
        /// Guarda una operación de crédito/débito entre cuentas.
        /// </summary>
        public static OperacionResult GuardarCreditoDebito(
            int cuentaCreditoId,
            int cuentaDebitoId,
            decimal montoCredito,
            decimal montoDebito,
            decimal cotizacion,
            int? clienteId = null,
            string observaciones = "")
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                var cuentaCredito = db.Cuentas.Find(cuentaCreditoId);
                var cuentaDebito = db.Cuentas.Find(cuentaDebitoId);

                if (cuentaCredito == null || cuentaDebito == null)
                    return OperacionResult.Error("Cuenta no encontrada");

                // VALIDACIÓN: La cuenta a debitar debe tener saldo suficiente
                if (cuentaDebito.Saldo < montoDebito)
                    return OperacionResult.Error(
                        $"Saldo insuficiente en '{cuentaDebito.Nombre}'. " +
                        $"Disponible: {cuentaDebito.Saldo:N2}");

                var operacion = new Operacion
                {
                    Fecha = DateTime.Now,
                    TipoOperacion = "Crédito/Débito",
                    ClienteId = clienteId,
                    MontoTotalOrigen = montoDebito,
                    MontoTotalDestino = montoCredito,
                    CotizacionAplicada = cotizacion,
                    Observaciones = observaciones
                };
                db.Operaciones.Add(operacion);

                // Crédito
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaCreditoId,
                    Monto = montoCredito
                });

                // Débito
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaDebitoId,
                    Monto = -montoDebito
                });

                cuentaCredito.Saldo += montoCredito;
                cuentaDebito.Saldo -= montoDebito;

                db.SaveChanges();
                transaction.Commit();

                AuditService.Registrar("CREATE", "Operacion", operacion.Id,
                    datosNuevos: new { tipo = "Crédito/Débito", montoCredito, montoDebito });

                return OperacionResult.Success(operacion.Id);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return OperacionResult.Error($"Error: {ex.Message}");
            }
        }
    }
}
