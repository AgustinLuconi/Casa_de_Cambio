using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using SistemaCambio.Services.Validators;
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

    public class OperacionService : IOperacionService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IAuditService _auditService;
        private readonly ICierreCajaService _cierreCajaService;
        private readonly OperacionValidator _validator;

        public OperacionService(
            IDbContextFactory<AppDbContext> contextFactory,
            IAuditService auditService,
            ICierreCajaService cierreCajaService,
            OperacionValidator validator)
        {
            _contextFactory = contextFactory;
            _auditService = auditService;
            _cierreCajaService = cierreCajaService;
            _validator = validator;
        }

        /// <summary>
        /// Guarda una operación de cambio con transacción atómica y validación de saldos.
        /// </summary>
        public OperacionResult GuardarOperacion(
            string tipo,
            int cuentaOrigenId,
            int cuentaDestinoId,
            string monedaOrigen,
            string monedaDestino,
            decimal montoOrigen,
            decimal montoDestino,
            decimal cotizacion,
            int? clienteId = null,
            string observaciones = "")
        {
            // ESTRATEGIA DE REDONDEO ESTRICTO (NUMSCRIPT UMN)
            montoOrigen = Math.Round(montoOrigen, 2, MidpointRounding.AwayFromZero);
            montoDestino = Math.Round(montoDestino, 2, MidpointRounding.AwayFromZero);
            cotizacion = Math.Round(cotizacion, 5, MidpointRounding.AwayFromZero);

            using var db = _contextFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                // VALIDACIÓN CENTRALIZADA
                var validacion = _validator.ValidarOperacion(
                    tipo, cuentaOrigenId, cuentaDestinoId,
                    monedaOrigen, monedaDestino,
                    montoOrigen, montoDestino, cotizacion);

                if (validacion.HasErrors)
                {
                    return OperacionResult.Error(
                        string.Join("\n", validacion.Errors.Select(e => $"• {e.Message}")));
                }

                // Verificar si el día está cerrado
                if (_cierreCajaService.HayDiaCerrado())
                {
                    return OperacionResult.Error(
                        "El día de hoy ya está cerrado. " +
                        "No se pueden realizar más operaciones hasta mañana.");
                }

                var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId);
                var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);

                if (cuentaOrigen == null)
                    return OperacionResult.Error("Cuenta origen no encontrada");
                if (cuentaDestino == null)
                    return OperacionResult.Error("Cuenta destino no encontrada");

                // Obtener o crear saldos por moneda
                var saldoOrigen = ObtenerOCrearSaldo(db, cuentaOrigenId, monedaOrigen);
                var saldoDestino = ObtenerOCrearSaldo(db, cuentaDestinoId, monedaDestino);

                // VALIDACIÓN CRÍTICA: Saldo no puede quedar negativo
                if (saldoOrigen.Saldo < montoOrigen)
                    return OperacionResult.Error(
                        $"Saldo insuficiente en '{cuentaOrigen.Nombre}' ({monedaOrigen}). " +
                        $"Disponible: {saldoOrigen.Saldo:N2}, Requerido: {montoOrigen:N2}");

                // CUENTA EXTERNA (MUNDO) PARA PARTIDA DOBLE COMPLETA (Banking-Expert)
                var cuentaExterna = ObtenerOCrearCuentaExterna(db);
                var saldoExternaOrigen = ObtenerOCrearSaldo(db, cuentaExterna.Id, monedaOrigen);
                var saldoExternaDestino = ObtenerOCrearSaldo(db, cuentaExterna.Id, monedaDestino);

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

                // ============= PATAS DE LA TRANSACCIÓN (SUMA CERO) ============= //

                // 1. DÉBITO Casa (sale dinero)
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaOrigenId,
                    Moneda = monedaOrigen,
                    Monto = -montoOrigen,
                    Fecha = DateTime.UtcNow
                });

                // 2. CRÉDITO Externa (entra el dinero de la casa al cliente)
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaExterna.Id,
                    Moneda = monedaOrigen,
                    Monto = montoOrigen,
                    Fecha = DateTime.UtcNow
                });

                // 3. DÉBITO Externa (sale el dinero destino del cliente)
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaExterna.Id,
                    Moneda = monedaDestino,
                    Monto = -montoDestino,
                    Fecha = DateTime.UtcNow
                });

                // 4. CRÉDITO Casa (entra el dinero del destino a la casa)
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaDestinoId,
                    Moneda = monedaDestino,
                    Monto = montoDestino,
                    Fecha = DateTime.UtcNow
                });

                // Actualizar saldos por moneda en casa
                saldoOrigen.Saldo -= montoOrigen;
                saldoDestino.Saldo += montoDestino;

                // Actualizar saldos por moneda externa
                saldoExternaOrigen.Saldo += montoOrigen;
                saldoExternaDestino.Saldo -= montoDestino;

                // Guardar todo en una transacción atómica
                db.SaveChanges();
                transaction.Commit();

                try { _auditService.Registrar("CREATE", "Operacion", operacion.Id,
                    datosNuevos: new { tipo, monedaOrigen, monedaDestino, montoOrigen, montoDestino, cotizacion }); } catch { }

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
        public OperacionResult GuardarCreditoDebito(
            int cuentaCreditoId,
            int cuentaDebitoId,
            string monedaCredito,
            string monedaDebito,
            decimal montoCredito,
            decimal montoDebito,
            decimal cotizacion,
            int? clienteId = null,
            string observaciones = "")
        {
            // ESTRATEGIA DE REDONDEO ESTRICTO (NUMSCRIPT UMN)
            montoCredito = Math.Round(montoCredito, 2, MidpointRounding.AwayFromZero);
            montoDebito = Math.Round(montoDebito, 2, MidpointRounding.AwayFromZero);
            cotizacion = Math.Round(cotizacion, 5, MidpointRounding.AwayFromZero);

            using var db = _contextFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                // VALIDACIÓN CENTRALIZADA
                var validacion = _validator.ValidarCreditoDebito(
                    cuentaCreditoId, cuentaDebitoId,
                    monedaCredito, monedaDebito,
                    montoCredito, montoDebito);

                if (validacion.HasErrors)
                {
                    return OperacionResult.Error(
                        string.Join("\n", validacion.Errors.Select(e => $"• {e.Message}")));
                }

                // ARBITRAJE AUTOMÁTICO: Si cruzan divisas internamente, requiere 4 patas (Zero-sum numscript constraint)
                if (monedaCredito != monedaDebito)
                {
                    return GuardarOperacionInterbancaria(
                        "Crédito/Débito",
                        cuentaOrigenId: cuentaDebitoId,
                        cuentaDestinoId: cuentaCreditoId,
                        monedaOrigen: monedaDebito,
                        monedaDestino: monedaCredito,
                        montoOrigen: montoDebito,
                        montoDestino: montoCredito,
                        cotizacion,
                        observaciones);
                }

                if (_cierreCajaService.HayDiaCerrado())
                {
                    return OperacionResult.Error(
                        "El día de hoy ya está cerrado. " +
                        "No se pueden realizar más operaciones hasta mañana.");
                }

                var cuentaCredito = db.Cuentas.Find(cuentaCreditoId);
                var cuentaDebito = db.Cuentas.Find(cuentaDebitoId);

                if (cuentaCredito == null || cuentaDebito == null)
                    return OperacionResult.Error("Cuenta no encontrada");

                // Obtener o crear saldos
                var saldoCredito = ObtenerOCrearSaldo(db, cuentaCreditoId, monedaCredito);
                var saldoDebito = ObtenerOCrearSaldo(db, cuentaDebitoId, monedaDebito);

                if (saldoDebito.Saldo < montoDebito)
                    return OperacionResult.Error(
                        $"Saldo insuficiente en '{cuentaDebito.Nombre}' ({monedaDebito}). " +
                        $"Disponible: {saldoDebito.Saldo:N2}");

                var operacion = new Operacion
                {
                    Fecha = DateTime.UtcNow,
                    TipoOperacion = "Crédito/Débito",
                    ClienteId = clienteId,
                    MontoTotalOrigen = montoDebito,
                    MontoTotalDestino = montoCredito,
                    CotizacionAplicada = cotizacion,
                    Observaciones = observaciones
                };
                db.Operaciones.Add(operacion);

                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaCreditoId,
                    Moneda = monedaCredito,
                    Monto = montoCredito,
                    Fecha = DateTime.UtcNow
                });

                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = cuentaDebitoId,
                    Moneda = monedaDebito,
                    Monto = -montoDebito,
                    Fecha = DateTime.UtcNow
                });

                saldoCredito.Saldo += montoCredito;
                saldoDebito.Saldo -= montoDebito;

                db.SaveChanges();
                transaction.Commit();

                _auditService.Registrar("CREATE", "Operacion", operacion.Id,
                    datosNuevos: new { tipo = "Crédito/Débito", montoCredito, montoDebito });

                return OperacionResult.Success(operacion.Id);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return OperacionResult.Error($"Error: {ex.Message}");
            }
        }
        /// <summary>
        /// Guarda una operación de Arbitraje (Interbancaria), moviendo 4 patas contables
        /// para cruzar de forma neta montos de MonedaA a MonedaB entre dos cuentas institucionales.
        /// </summary>
        public OperacionResult GuardarOperacionInterbancaria(
            string tipo,
            int cuentaOrigenId,
            int cuentaDestinoId,
            string monedaOrigen,
            string monedaDestino,
            decimal montoOrigen,
            decimal montoDestino,
            decimal cotizacion,
            string observaciones = "")
        {
            // ESTRATEGIA DE REDONDEO ESTRICTO (NUMSCRIPT UMN)
            montoOrigen = Math.Round(montoOrigen, 2, MidpointRounding.AwayFromZero);
            montoDestino = Math.Round(montoDestino, 2, MidpointRounding.AwayFromZero);
            cotizacion = Math.Round(cotizacion, 5, MidpointRounding.AwayFromZero);

            using var db = _contextFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                var validacion = _validator.ValidarOperacionInterbancaria(
                    cuentaOrigenId, cuentaDestinoId,
                    monedaOrigen, monedaDestino,
                    montoOrigen, montoDestino);

                if (validacion.HasErrors)
                {
                    return OperacionResult.Error(
                        string.Join("\n", validacion.Errors.Select(e => $"• {e.Message}")));
                }

                if (_cierreCajaService.HayDiaCerrado())
                    return OperacionResult.Error("El día de hoy ya está cerrado.");

                var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId);
                var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);

                var dbSaldoOrigenA = ObtenerOCrearSaldo(db, cuentaOrigenId, monedaOrigen);
                var dbSaldoOrigenB = ObtenerOCrearSaldo(db, cuentaOrigenId, monedaDestino);
                var dbSaldoDestinoB = ObtenerOCrearSaldo(db, cuentaDestinoId, monedaDestino);
                var dbSaldoDestinoA = ObtenerOCrearSaldo(db, cuentaDestinoId, monedaOrigen);

                var operacion = new Operacion
                {
                    Fecha = DateTime.UtcNow,
                    TipoOperacion = "Interbancaria",
                    ClienteId = null,
                    MontoTotalOrigen = montoOrigen,
                    MontoTotalDestino = montoDestino,
                    CotizacionAplicada = cotizacion,
                    Observaciones = observaciones
                };
                db.Operaciones.Add(operacion);

                // 1. Origen pierde Moneda A
                db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaOrigenId, Moneda = monedaOrigen, Monto = -montoOrigen, Fecha = DateTime.UtcNow });
                dbSaldoOrigenA.Saldo -= montoOrigen;

                // 2. Origen gana Moneda B
                db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaOrigenId, Moneda = monedaDestino, Monto = montoDestino, Fecha = DateTime.UtcNow });
                dbSaldoOrigenB.Saldo += montoDestino;

                // 3. Destino pierde Moneda B
                db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaDestinoId, Moneda = monedaDestino, Monto = -montoDestino, Fecha = DateTime.UtcNow });
                dbSaldoDestinoB.Saldo -= montoDestino;

                // 4. Destino gana Moneda A
                db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaDestinoId, Moneda = monedaOrigen, Monto = montoOrigen, Fecha = DateTime.UtcNow });
                dbSaldoDestinoA.Saldo += montoOrigen;

                db.SaveChanges();
                transaction.Commit();

                try { _auditService.Registrar("CREATE", "Operacion_Interbancaria", operacion.Id, new { cuentaOrigenId, cuentaDestinoId, montoOrigen, montoDestino }); } catch { }

                return OperacionResult.Success(operacion.Id);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return OperacionResult.Error($"Error: {ex.Message}");
            }
        }
        /// <summary>
        /// Busca el SaldoCuenta para la combinación cuenta+moneda.
        /// Si no existe, lo crea con saldo 0.
        /// </summary>
        private SaldoCuenta ObtenerOCrearSaldo(AppDbContext db, int cuentaId, string moneda)
        {
            var saldo = db.SaldosCuenta
                .FirstOrDefault(s => s.CuentaId == cuentaId && s.Moneda == moneda);

            if (saldo == null)
            {
                saldo = new SaldoCuenta
                {
                    CuentaId = cuentaId,
                    Moneda = moneda,
                    Saldo = 0
                };
                db.SaldosCuenta.Add(saldo);
                db.SaveChanges(); // Para obtener el Id
            }

            return saldo;
        }

        /// <summary>
        /// Obtiene o crea la cuenta "Mundo Exterior" universal para el registro Double-Entry de clientes.
        /// </summary>
        private Cuenta ObtenerOCrearCuentaExterna(AppDbContext db)
        {
            var cuenta = db.Cuentas.FirstOrDefault(c => c.Nombre == "Mundo Exterior" && c.Tipo == "Externo");
            if (cuenta == null)
            {
                cuenta = new Cuenta { Nombre = "Mundo Exterior", Tipo = "Externo" };
                db.Cuentas.Add(cuenta);
                db.SaveChanges(); // Persiste el ID
            }
            return cuenta;
        }
    }
}
