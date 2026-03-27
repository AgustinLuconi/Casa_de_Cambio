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

    public OperacionResult GuardarOperacion(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, int? clienteId = null, string observaciones = "")
    {
        montoOrigen = Math.Round(montoOrigen, 2, MidpointRounding.AwayFromZero);
        montoDestino = Math.Round(montoDestino, 2, MidpointRounding.AwayFromZero);
        cotizacion = Math.Round(cotizacion, 5, MidpointRounding.AwayFromZero);
        using var db = _contextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var validacion = _validator.ValidarOperacion(tipo, cuentaOrigenId, cuentaDestinoId, monedaOrigen, monedaDestino, montoOrigen, montoDestino, cotizacion);
            if (validacion.HasErrors) return OperacionResult.Error(string.Join("\n", validacion.Errors.Select(e => $"• {e.Message}")));
            if (_cierreCajaService.HayDiaCerrado()) return OperacionResult.Error("El dia de hoy ya esta cerrado. No se pueden realizar mas operaciones hasta mañana.");
            var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId);
            var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);
            if (cuentaOrigen == null) return OperacionResult.Error("Cuenta origen no encontrada");
            if (cuentaDestino == null) return OperacionResult.Error("Cuenta destino no encontrada");
            var saldoOrigen = ObtenerOCrearSaldo(db, cuentaOrigenId, monedaOrigen);
            var saldoDestino = ObtenerOCrearSaldo(db, cuentaDestinoId, monedaDestino);
            if (saldoOrigen.Saldo < montoOrigen)
            {
                decimal limiteDeuda = ObtenerLimiteDeuda(db, cuentaOrigen);
                if (limiteDeuda > 0)
                {
                    decimal saldoProyectado = saldoOrigen.Saldo - montoOrigen;
                    if (saldoProyectado < -limiteDeuda)
                        return OperacionResult.Error($"La cuenta '{cuentaOrigen.Nombre}' superaría su límite de deuda ({limiteDeuda:N2}).\nSaldo actual: {saldoOrigen.Saldo:N2}, Requerido: {montoOrigen:N2}");
                }
                else
                {
                    return OperacionResult.Error($"Saldo insuficiente en '{cuentaOrigen.Nombre}' ({monedaOrigen}). Disponible: {saldoOrigen.Saldo:N2}, Requerido: {montoOrigen:N2}");
                }
            }
            var operacion = new Operacion { Fecha = DateTime.UtcNow, TipoOperacion = tipo, ClienteId = clienteId, MontoTotalOrigen = montoOrigen, MontoTotalDestino = montoDestino, CotizacionAplicada = cotizacion, Observaciones = observaciones };
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

    public OperacionResult GuardarCreditoDebito(int cuentaCreditoId, int cuentaDebitoId, string monedaCredito, string monedaDebito, decimal montoCredito, decimal montoDebito, decimal cotizacion, int? clienteId = null, string observaciones = "")
    {
        montoCredito = Math.Round(montoCredito, 2, MidpointRounding.AwayFromZero);
        montoDebito = Math.Round(montoDebito, 2, MidpointRounding.AwayFromZero);
        cotizacion = Math.Round(cotizacion, 5, MidpointRounding.AwayFromZero);
        using var db = _contextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var validacion = _validator.ValidarCreditoDebito(cuentaCreditoId, cuentaDebitoId, monedaCredito, monedaDebito, montoCredito, montoDebito);
            if (validacion.HasErrors) return OperacionResult.Error(string.Join("\n", validacion.Errors.Select(e => $"• {e.Message}")));
            if (monedaCredito != monedaDebito) return GuardarOperacionInterbancaria("Credito/Debito", cuentaOrigenId: cuentaDebitoId, cuentaDestinoId: cuentaCreditoId, monedaOrigen: monedaDebito, monedaDestino: monedaCredito, montoOrigen: montoDebito, montoDestino: montoCredito, cotizacion, observaciones);
            if (_cierreCajaService.HayDiaCerrado()) return OperacionResult.Error("El dia de hoy ya esta cerrado.");
            var cuentaCredito = db.Cuentas.Find(cuentaCreditoId);
            var cuentaDebito2 = db.Cuentas.Find(cuentaDebitoId);
            if (cuentaCredito == null || cuentaDebito2 == null) return OperacionResult.Error("Cuenta no encontrada");
            var saldoCredito = ObtenerOCrearSaldo(db, cuentaCreditoId, monedaCredito);
            var saldoDebito = ObtenerOCrearSaldo(db, cuentaDebitoId, monedaDebito);
            if (saldoDebito.Saldo < montoDebito)
            {
                decimal limiteDeuda = ObtenerLimiteDeuda(db, cuentaDebito2);
                if (limiteDeuda > 0)
                {
                    decimal saldoProyectado = saldoDebito.Saldo - montoDebito;
                    if (saldoProyectado < -limiteDeuda)
                        return OperacionResult.Error($"La cuenta '{cuentaDebito2.Nombre}' superaría su límite de deuda ({limiteDeuda:N2}).\nSaldo actual: {saldoDebito.Saldo:N2}, Requerido: {montoDebito:N2}");
                }
                else
                {
                    return OperacionResult.Error($"Saldo insuficiente en '{cuentaDebito2.Nombre}' ({monedaDebito}). Disponible: {saldoDebito.Saldo:N2}");
                }
            }
            var operacion = new Operacion { Fecha = DateTime.UtcNow, TipoOperacion = "Credito/Debito", ClienteId = clienteId, MontoTotalOrigen = montoDebito, MontoTotalDestino = montoCredito, CotizacionAplicada = cotizacion, Observaciones = observaciones };
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

    public OperacionResult GuardarOperacionInterbancaria(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, string observaciones = "")
    {
        montoOrigen = Math.Round(montoOrigen, 2, MidpointRounding.AwayFromZero);
        montoDestino = Math.Round(montoDestino, 2, MidpointRounding.AwayFromZero);
        cotizacion = Math.Round(cotizacion, 5, MidpointRounding.AwayFromZero);
        using var db = _contextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var validacion = _validator.ValidarOperacionInterbancaria(cuentaOrigenId, cuentaDestinoId, monedaOrigen, monedaDestino, montoOrigen, montoDestino);
            if (validacion.HasErrors) return OperacionResult.Error(string.Join("\n", validacion.Errors.Select(e => $"• {e.Message}")));
            if (_cierreCajaService.HayDiaCerrado()) return OperacionResult.Error("El dia de hoy ya esta cerrado.");
            var cuentaOrigen = db.Cuentas.Find(cuentaOrigenId); var cuentaDestino = db.Cuentas.Find(cuentaDestinoId);
            var dbSaldoOrigenA = ObtenerOCrearSaldo(db, cuentaOrigenId, monedaOrigen);
            var dbSaldoOrigenB = ObtenerOCrearSaldo(db, cuentaOrigenId, monedaDestino);
            var dbSaldoDestinoB = ObtenerOCrearSaldo(db, cuentaDestinoId, monedaDestino);
            var dbSaldoDestinoA = ObtenerOCrearSaldo(db, cuentaDestinoId, monedaOrigen);
            var operacion = new Operacion { Fecha = DateTime.UtcNow, TipoOperacion = "Interbancaria", MontoTotalOrigen = montoOrigen, MontoTotalDestino = montoDestino, CotizacionAplicada = cotizacion, Observaciones = observaciones };
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

    private decimal ObtenerLimiteDeuda(AppDbContext db, Cuenta cuenta)
    {
        if (cuenta.Tipo != "Cliente") return 0;
        if (cuenta.LimiteDeuda.HasValue && cuenta.LimiteDeuda.Value > 0)
            return cuenta.LimiteDeuda.Value;
        var config = db.ConfiguracionSistema.Find("limite_deuda_general");
        if (config != null && decimal.TryParse(config.Valor,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var limGeneral)
            && limGeneral > 0)
            return limGeneral;
        return 0;
    }

    private SaldoCuenta ObtenerOCrearSaldo(AppDbContext db, int cuentaId, string moneda)
    {
        var saldo = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaId && s.Moneda == moneda);
        if (saldo == null) { saldo = new SaldoCuenta { CuentaId = cuentaId, Moneda = moneda, Saldo = 0 }; db.SaldosCuenta.Add(saldo); db.SaveChanges(); }
        return saldo;
    }

}
