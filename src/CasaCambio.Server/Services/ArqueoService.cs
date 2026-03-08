using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Server.Models;

namespace CasaCambio.Server.Services;

public class ArqueoService : IArqueoService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;
    public ArqueoService(IDbContextFactory<AppDbContext> contextFactory, IAuditService auditService)
    { _contextFactory = contextFactory; _auditService = auditService; }

    public ArqueoResult RealizarArqueoCiego(int cuentaId, string moneda, decimal montoContado, string observaciones = "")
    {
        using var db = _contextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var cuenta = db.Cuentas.Find(cuentaId);
            if (cuenta == null) return ArqueoResult.Error("Cuenta no encontrada");
            var saldoCuenta = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaId && s.Moneda == moneda);
            decimal saldoSistema = saldoCuenta?.Saldo ?? 0;
            decimal diferencia = montoContado - saldoSistema;
            var arqueo = new Arqueo { CuentaId = cuentaId, Fecha = DateTime.Now, SaldoSistema = saldoSistema, SaldoArqueo = montoContado, Diferencia = diferencia, Observaciones = string.IsNullOrEmpty(observaciones) ? (diferencia == 0 ? "Cuadra" : (diferencia > 0 ? "Sobrante" : "Faltante")) : observaciones };
            db.Arqueos.Add(arqueo); db.SaveChanges();
            if (diferencia != 0)
            {
                var cuentaAjuste = db.Cuentas.FirstOrDefault(c => c.Nombre == "Diferencias de Caja");
                if (cuentaAjuste == null) { cuentaAjuste = new Cuenta { Nombre = "Diferencias de Caja", Tipo = "Resultado" }; db.Cuentas.Add(cuentaAjuste); db.SaveChanges(); }
                var tipoAjuste = diferencia > 0 ? "Sobrante Caja" : "Faltante Caja";
                var opAjuste = new Operacion { Fecha = DateTime.Now, TipoOperacion = tipoAjuste, MontoTotalOrigen = Math.Abs(diferencia), MontoTotalDestino = Math.Abs(diferencia), CotizacionAplicada = 1, Observaciones = $"Ajuste automatico por arqueo #{arqueo.Id}" };
                db.Operaciones.Add(opAjuste);
                db.Movimientos.Add(new Movimiento { Operacion = opAjuste, CuentaId = cuentaId, Monto = diferencia, Fecha = DateTime.Now });
                db.Movimientos.Add(new Movimiento { Operacion = opAjuste, CuentaId = cuentaAjuste.Id, Monto = -diferencia, Fecha = DateTime.Now });
                if (saldoCuenta != null) saldoCuenta.Saldo = montoContado;
                else db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = cuentaId, Moneda = moneda, Saldo = montoContado });
                var saldoAjuste = db.SaldosCuenta.FirstOrDefault(s => s.CuentaId == cuentaAjuste.Id && s.Moneda == moneda);
                if (saldoAjuste != null) saldoAjuste.Saldo -= diferencia;
                else db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = cuentaAjuste.Id, Moneda = moneda, Saldo = -diferencia });
                db.SaveChanges();
                arqueo.MovimientoAjusteId = db.Movimientos.Where(m => m.Operacion == opAjuste && m.CuentaId == cuentaId).Select(m => m.Id).FirstOrDefault();
                _auditService.Registrar("AJUSTE", "Arqueo", arqueo.Id, datosNuevos: new { diferencia, tipoAjuste, moneda, saldoAnterior = saldoSistema, saldoNuevo = montoContado });
            }
            db.SaveChanges(); transaction.Commit();
            return ArqueoResult.Success(arqueo.Id, diferencia);
        }
        catch (Exception ex) { transaction.Rollback(); return ArqueoResult.Error($"Error al realizar arqueo: {ex.Message}"); }
    }
}
