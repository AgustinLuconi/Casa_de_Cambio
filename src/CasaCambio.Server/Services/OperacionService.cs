using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Server.Models;
using CasaCambio.Server.Validators;

namespace CasaCambio.Server.Services;

public class OperacionService : IOperacionService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;
    private readonly ICierreCajaService _cierreCajaService;
    private readonly OperacionValidator _validator;

    public OperacionService(IDbContextFactory<AppDbContext> contextFactory, IAuditService auditService, ICierreCajaService cierreCajaService, OperacionValidator validator)
    { _contextFactory = contextFactory; _auditService = auditService; _cierreCajaService = cierreCajaService; _validator = validator; }

    public OperacionResult GuardarOperacion(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, string observaciones = "", string? idempotencyKey = null)
    {
        montoOrigen = Math.Round(montoOrigen, 2, MidpointRounding.AwayFromZero);
        montoDestino = Math.Round(montoDestino, 2, MidpointRounding.AwayFromZero);
        cotizacion = Math.Round(cotizacion, 5, MidpointRounding.AwayFromZero);
        using var db = _contextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            if (idempotencyKey != null)
            {
                var existente = db.Operaciones.AsNoTracking().FirstOrDefault(o => o.IdempotencyKey == idempotencyKey);
                if (existente != null) return OperacionResult.Success(existente.Id);
            }
            var validacion = _validator.ValidarOperacion(tipo, cuentaOrigenId, cuentaDestinoId, monedaOrigen, monedaDestino, montoOrigen, montoDestino, cotizacion);
            if (validacion.HasErrors) return OperacionResult.Error(string.Join("\n", validacion.Errors.Select(e => $"• {e.Message}")));
            if (_cierreCajaService.HayDiaCerrado()) return OperacionResult.Error("El dia de hoy ya esta cerrado. No se pueden realizar mas operaciones hasta mañana.");
            var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId);
            var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);
            if (cuentaOrigen == null) return OperacionResult.Error("Cuenta origen no encontrada");
            if (cuentaDestino == null) return OperacionResult.Error("Cuenta destino no encontrada");
            var errOrigen = ValidarMonoMonedaEfectivo(db, cuentaOrigenId, monedaOrigen);
            if (errOrigen != null) return errOrigen;
            var errDestino = ValidarMonoMonedaEfectivo(db, cuentaDestinoId, monedaDestino);
            if (errDestino != null) return errDestino;
            var saldoOrigen = ObtenerOCrearSaldo(db, cuentaOrigenId, monedaOrigen);
            var saldoDestino = ObtenerOCrearSaldo(db, cuentaDestinoId, monedaDestino);
            if (saldoOrigen.Saldo < montoOrigen)
            {
                decimal limiteDeuda = ObtenerLimiteDeuda(db, cuentaOrigen, saldoOrigen);
                if (limiteDeuda > 0)
                {
                    decimal saldoProyectado = saldoOrigen.Saldo - montoOrigen;
                    if (saldoProyectado < -limiteDeuda)
                        return OperacionResult.Error($"La cuenta '{cuentaOrigen.Nombre}' superaría su límite de deuda en {monedaOrigen} ({limiteDeuda:N2}).\nSaldo actual: {saldoOrigen.Saldo:N2}, Requerido: {montoOrigen:N2}");
                }
                else
                {
                    return OperacionResult.Error($"Saldo insuficiente en '{cuentaOrigen.Nombre}' ({monedaOrigen}). Disponible: {saldoOrigen.Saldo:N2}, Requerido: {montoOrigen:N2}");
                }
            }
            var operacion = new Operacion { Fecha = DateTime.UtcNow, TipoOperacion = tipo, MontoTotalOrigen = montoOrigen, MontoTotalDestino = montoDestino, CotizacionAplicada = cotizacion, Observaciones = observaciones, IdempotencyKey = idempotencyKey };
            db.Operaciones.Add(operacion);
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaOrigenId, Moneda = monedaOrigen, Monto = -montoOrigen, Fecha = DateTime.UtcNow });
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaDestinoId, Moneda = monedaDestino, Monto = montoDestino, Fecha = DateTime.UtcNow });
            saldoOrigen.Saldo -= montoOrigen; saldoDestino.Saldo += montoDestino;
            db.SaveChanges(); transaction.Commit();
            try { _auditService.Registrar("CREATE", "Operacion", operacion.Id, datosNuevos: new { tipo, monedaOrigen, monedaDestino, montoOrigen, montoDestino, cotizacion }); } catch { }
            return OperacionResult.Success(operacion.Id);
        }
        catch (Exception ex) { transaction.Rollback(); return OperacionResult.Error($"Error al guardar operacion: {ex.InnerException?.Message ?? ex.Message}"); }
    }

    public OperacionResult GuardarCreditoDebito(int cuentaCreditoId, int cuentaDebitoId, string monedaCredito, string monedaDebito, decimal montoCredito, decimal montoDebito, decimal cotizacion, string observaciones = "", string? idempotencyKey = null)
    {
        montoCredito = Math.Round(montoCredito, 2, MidpointRounding.AwayFromZero);
        montoDebito = Math.Round(montoDebito, 2, MidpointRounding.AwayFromZero);
        cotizacion = Math.Round(cotizacion, 5, MidpointRounding.AwayFromZero);
        using var db = _contextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            if (idempotencyKey != null)
            {
                var existente = db.Operaciones.AsNoTracking().FirstOrDefault(o => o.IdempotencyKey == idempotencyKey);
                if (existente != null) return OperacionResult.Success(existente.Id);
            }
            var validacion = _validator.ValidarCreditoDebito(cuentaCreditoId, cuentaDebitoId, monedaCredito, monedaDebito, montoCredito, montoDebito);
            if (validacion.HasErrors) return OperacionResult.Error(string.Join("\n", validacion.Errors.Select(e => $"• {e.Message}")));
            if (monedaCredito != monedaDebito) return GuardarOperacionInterbancaria("Credito/Debito", cuentaOrigenId: cuentaDebitoId, cuentaDestinoId: cuentaCreditoId, monedaOrigen: monedaDebito, monedaDestino: monedaCredito, montoOrigen: montoDebito, montoDestino: montoCredito, cotizacion, observaciones, idempotencyKey);
            if (_cierreCajaService.HayDiaCerrado()) return OperacionResult.Error("El dia de hoy ya esta cerrado.");
            var cuentaCredito = db.Cuentas.Find(cuentaCreditoId);
            var cuentaDebito2 = db.Cuentas.Find(cuentaDebitoId);
            if (cuentaCredito == null || cuentaDebito2 == null) return OperacionResult.Error("Cuenta no encontrada");
            var saldoCredito = ObtenerOCrearSaldo(db, cuentaCreditoId, monedaCredito);
            var saldoDebito = ObtenerOCrearSaldo(db, cuentaDebitoId, monedaDebito);
            if (saldoDebito.Saldo < montoDebito)
            {
                decimal limiteDeuda = ObtenerLimiteDeuda(db, cuentaDebito2, saldoDebito);
                if (limiteDeuda > 0)
                {
                    decimal saldoProyectado = saldoDebito.Saldo - montoDebito;
                    if (saldoProyectado < -limiteDeuda)
                        return OperacionResult.Error($"La cuenta '{cuentaDebito2.Nombre}' superaría su límite de deuda en {monedaDebito} ({limiteDeuda:N2}).\nSaldo actual: {saldoDebito.Saldo:N2}, Requerido: {montoDebito:N2}");
                }
                else
                {
                    return OperacionResult.Error($"Saldo insuficiente en '{cuentaDebito2.Nombre}' ({monedaDebito}). Disponible: {saldoDebito.Saldo:N2}");
                }
            }
            var operacion = new Operacion { Fecha = DateTime.UtcNow, TipoOperacion = "Credito/Debito", MontoTotalOrigen = montoDebito, MontoTotalDestino = montoCredito, CotizacionAplicada = cotizacion, Observaciones = observaciones, IdempotencyKey = idempotencyKey };
            db.Operaciones.Add(operacion);
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaCreditoId, Moneda = monedaCredito, Monto = montoCredito, Fecha = DateTime.UtcNow });
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaDebitoId, Moneda = monedaDebito, Monto = -montoDebito, Fecha = DateTime.UtcNow });
            saldoCredito.Saldo += montoCredito; saldoDebito.Saldo -= montoDebito;
            db.SaveChanges(); transaction.Commit();
            _auditService.Registrar("CREATE", "Operacion", operacion.Id, datosNuevos: new { tipo = "Credito/Debito", montoCredito, montoDebito });
            return OperacionResult.Success(operacion.Id);
        }
        catch (Exception ex) { transaction.Rollback(); return OperacionResult.Error($"Error: {ex.Message}"); }
    }

    // Sin ventana propia en el desktop actual — solo se alcanza vía el endpoint
    // api/operaciones/interbancaria (sin UI que lo llame) o internamente desde
    // GuardarCreditoDebito cuando monedaCredito != monedaDebito (rama hoy inalcanzable,
    // ya que CreditoDebitoWindow usa un único selector de moneda para ambos lados).
    public OperacionResult GuardarOperacionInterbancaria(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, string observaciones = "", string? idempotencyKey = null)
    {
        montoOrigen = Math.Round(montoOrigen, 2, MidpointRounding.AwayFromZero);
        montoDestino = Math.Round(montoDestino, 2, MidpointRounding.AwayFromZero);
        cotizacion = Math.Round(cotizacion, 5, MidpointRounding.AwayFromZero);
        using var db = _contextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            if (idempotencyKey != null)
            {
                var existente = db.Operaciones.AsNoTracking().FirstOrDefault(o => o.IdempotencyKey == idempotencyKey);
                if (existente != null) return OperacionResult.Success(existente.Id);
            }
            var validacion = _validator.ValidarOperacionInterbancaria(cuentaOrigenId, cuentaDestinoId, monedaOrigen, monedaDestino, montoOrigen, montoDestino);
            if (validacion.HasErrors) return OperacionResult.Error(string.Join("\n", validacion.Errors.Select(e => $"• {e.Message}")));
            if (_cierreCajaService.HayDiaCerrado()) return OperacionResult.Error("El dia de hoy ya esta cerrado.");
            var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId); var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);
            if (cuentaOrigen == null) return OperacionResult.Error("Cuenta origen no encontrada");
            if (cuentaDestino == null) return OperacionResult.Error("Cuenta destino no encontrada");
            var dbSaldoOrigenA = ObtenerOCrearSaldo(db, cuentaOrigenId, monedaOrigen);
            var dbSaldoOrigenB = ObtenerOCrearSaldo(db, cuentaOrigenId, monedaDestino);
            var dbSaldoDestinoB = ObtenerOCrearSaldo(db, cuentaDestinoId, monedaDestino);
            var dbSaldoDestinoA = ObtenerOCrearSaldo(db, cuentaDestinoId, monedaOrigen);

            // Validar saldo/límite de deuda en los dos lados que se debitan (misma regla que GuardarOperacion/GuardarCreditoDebito)
            if (dbSaldoOrigenA.Saldo < montoOrigen)
            {
                decimal limiteDeuda = ObtenerLimiteDeuda(db, cuentaOrigen, dbSaldoOrigenA);
                if (limiteDeuda > 0)
                {
                    decimal saldoProyectado = dbSaldoOrigenA.Saldo - montoOrigen;
                    if (saldoProyectado < -limiteDeuda)
                        return OperacionResult.Error($"La cuenta '{cuentaOrigen.Nombre}' superaría su límite de deuda en {monedaOrigen} ({limiteDeuda:N2}).\nSaldo actual: {dbSaldoOrigenA.Saldo:N2}, Requerido: {montoOrigen:N2}");
                }
                else
                {
                    return OperacionResult.Error($"Saldo insuficiente en '{cuentaOrigen.Nombre}' ({monedaOrigen}). Disponible: {dbSaldoOrigenA.Saldo:N2}, Requerido: {montoOrigen:N2}");
                }
            }
            if (dbSaldoDestinoB.Saldo < montoDestino)
            {
                decimal limiteDeuda = ObtenerLimiteDeuda(db, cuentaDestino, dbSaldoDestinoB);
                if (limiteDeuda > 0)
                {
                    decimal saldoProyectado = dbSaldoDestinoB.Saldo - montoDestino;
                    if (saldoProyectado < -limiteDeuda)
                        return OperacionResult.Error($"La cuenta '{cuentaDestino.Nombre}' superaría su límite de deuda en {monedaDestino} ({limiteDeuda:N2}).\nSaldo actual: {dbSaldoDestinoB.Saldo:N2}, Requerido: {montoDestino:N2}");
                }
                else
                {
                    return OperacionResult.Error($"Saldo insuficiente en '{cuentaDestino.Nombre}' ({monedaDestino}). Disponible: {dbSaldoDestinoB.Saldo:N2}, Requerido: {montoDestino:N2}");
                }
            }

            var operacion = new Operacion { Fecha = DateTime.UtcNow, TipoOperacion = "Interbancaria", MontoTotalOrigen = montoOrigen, MontoTotalDestino = montoDestino, CotizacionAplicada = cotizacion, Observaciones = observaciones, IdempotencyKey = idempotencyKey };
            db.Operaciones.Add(operacion);
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaOrigenId, Moneda = monedaOrigen, Monto = -montoOrigen, Fecha = DateTime.UtcNow });
            dbSaldoOrigenA.Saldo -= montoOrigen;
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaOrigenId, Moneda = monedaDestino, Monto = montoDestino, Fecha = DateTime.UtcNow });
            dbSaldoOrigenB.Saldo += montoDestino;
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaDestinoId, Moneda = monedaDestino, Monto = -montoDestino, Fecha = DateTime.UtcNow });
            dbSaldoDestinoB.Saldo -= montoDestino;
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = cuentaDestinoId, Moneda = monedaOrigen, Monto = montoOrigen, Fecha = DateTime.UtcNow });
            dbSaldoDestinoA.Saldo += montoOrigen;
            db.SaveChanges(); transaction.Commit();
            try { _auditService.Registrar("CREATE", "Operacion_Interbancaria", operacion.Id, new { cuentaOrigenId, cuentaDestinoId, montoOrigen, montoDestino }); } catch { }
            return OperacionResult.Success(operacion.Id);
        }
        catch (Exception ex) { transaction.Rollback(); return OperacionResult.Error($"Error: {ex.Message}"); }
    }

    /// <summary>
    /// Resuelve el límite de deuda aplicable a una cuenta Cliente para UNA divisa concreta.
    /// Cadena de herencia:
    ///   1. Límite específico de la cuenta para esa divisa (saldos_cuenta.limite_deuda)
    ///   2. Límite global por divisa (configuracion: limite_deuda_general_{MONEDA})
    ///   3. Legacy — límite escalar de la cuenta (cuentas.limite_deuda, pre-refactor)
    ///   4. Legacy — límite global único (configuracion: limite_deuda_general)
    ///   5. 0 = sin límite (la operación a descubierto se rechaza)
    /// </summary>
    private decimal ObtenerLimiteDeuda(AppDbContext db, Cuenta cuenta, SaldoCuenta saldo)
    {
        if (cuenta.Tipo != "Cliente") return 0;

        // 1. Límite específico cuenta+divisa
        if (saldo.LimiteDeuda > 0)
            return saldo.LimiteDeuda;

        // 2. Límite global de la divisa
        if (LeerLimiteConfig(db, $"limite_deuda_general_{saldo.Moneda}") is decimal porDivisa)
            return porDivisa;

        // 3. Legacy: escalar de la cuenta (compatibilidad con datos pre-refactor)
        if (cuenta.LimiteDeuda.HasValue && cuenta.LimiteDeuda.Value > 0)
            return cuenta.LimiteDeuda.Value;

        // 4. Legacy: escalar global único
        if (LeerLimiteConfig(db, "limite_deuda_general") is decimal global)
            return global;

        return 0;
    }

    private static decimal? LeerLimiteConfig(AppDbContext db, string clave)
    {
        var config = db.ConfiguracionSistema.Find(clave);
        if (config == null) return null;
        if (!decimal.TryParse(config.Valor,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var limite))
            return null;
        // 0 = sin límite (ilimitado); > 0 = límite específico; < 0 = no configurado
        return limite >= 0 ? (limite == 0 ? decimal.MaxValue : limite) : null;
    }

    private OperacionResult? ValidarMonoMonedaEfectivo(AppDbContext db, int cuentaId, string moneda)
    {
        var cuenta = db.Cuentas.Find(cuentaId);
        if (cuenta?.Tipo != "Efectivo") return null;
        var monedaExistente = db.SaldosCuenta
            .Where(s => s.CuentaId == cuentaId)
            .Select(s => s.Moneda)
            .FirstOrDefault();
        if (monedaExistente != null && monedaExistente != moneda)
            return OperacionResult.Error($"La caja '{cuenta.Nombre}' es mono-moneda ({monedaExistente}). No puede operar en {moneda}.");
        return null;
    }

    public OperacionResult AnularOperacion(int id)
    {
        using var db = _contextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var original = db.Operaciones.Include(o => o.Movimientos).FirstOrDefault(o => o.Id == id);
            if (original == null) return OperacionResult.Error("Operación no encontrada.");
            if (original.Anulada) return OperacionResult.Error("La operación ya fue anulada.");
            if (original.OperacionOriginalId.HasValue) return OperacionResult.Error("No se puede anular una anulación.");
            if (_cierreCajaService.HayDiaCerrado()) return OperacionResult.Error("El día de hoy ya está cerrado.");

            var anulacion = new Operacion
            {
                Fecha = DateTime.UtcNow,
                TipoOperacion = "Anulacion",
                MontoTotalOrigen = original.MontoTotalOrigen,
                MontoTotalDestino = original.MontoTotalDestino,
                CotizacionAplicada = original.CotizacionAplicada,
                Observaciones = $"ANULACIÓN DE OP-{id:D5}",
                OperacionOriginalId = id
            };
            db.Operaciones.Add(anulacion);

            foreach (var mov in original.Movimientos)
            {
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = anulacion,
                    CuentaId = mov.CuentaId,
                    Moneda = mov.Moneda,
                    Monto = -mov.Monto,
                    Fecha = DateTime.UtcNow
                });
                var saldo = ObtenerOCrearSaldo(db, mov.CuentaId, mov.Moneda);
                saldo.Saldo -= mov.Monto;
            }

            original.Anulada = true;
            db.SaveChanges();
            transaction.Commit();
            try { _auditService.Registrar("ANULAR", "Operacion", id, datosNuevos: new { anulacion_id = anulacion.Id }); } catch { }
            return OperacionResult.Success(anulacion.Id);
        }
        catch (Exception ex) { transaction.Rollback(); return OperacionResult.Error($"Error al anular: {ex.InnerException?.Message ?? ex.Message}"); }
    }

    private SaldoCuenta ObtenerOCrearSaldo(AppDbContext db, int cuentaId, string moneda)
    {
        var saldo = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaId && s.Moneda == moneda);
        if (saldo == null) { saldo = new SaldoCuenta { CuentaId = cuentaId, Moneda = moneda, Saldo = 0 }; db.SaldosCuenta.Add(saldo); db.SaveChanges(); }
        return saldo;
    }
}
