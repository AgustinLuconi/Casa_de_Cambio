using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Server.Models;

namespace CasaCambio.Server.Services;

public class CierreCajaService : ICierreCajaService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;

    public CierreCajaService(IDbContextFactory<AppDbContext> contextFactory, IAuditService auditService)
    { _contextFactory = contextFactory; _auditService = auditService; }

    public CierreResult GenerarCierre(string observaciones = "")
    {
        using var db = _contextFactory.CreateDbContext();
        var hoy = DateTime.UtcNow.Date;
        var cierreExistente = db.CierresCaja.FirstOrDefault(c => c.Fecha >= hoy && c.Fecha < hoy.AddDays(1));
        if (cierreExistente != null && cierreExistente.Cerrado)
            return CierreResult.Error($"Ya existe un cierre cerrado para el dia {hoy:dd/MM/yyyy}.");

        var cierre = new CierreCaja { Fecha = hoy, FechaCierre = DateTime.Now, Usuario = "Admin", Observaciones = observaciones };
        var inicioHoy = hoy; var finHoy = hoy.AddDays(1);
        var operacionesHoy = db.Operaciones.Where(o => o.Fecha >= inicioHoy && o.Fecha < finHoy).ToList();
        var compras = operacionesHoy.Where(o => o.TipoOperacion == "Compra").ToList();
        cierre.CantidadCompras = compras.Count;
        cierre.TotalComprasUSD = compras.Sum(o => o.MontoTotalDestino);
        cierre.TotalComprasARS = compras.Sum(o => o.MontoTotalOrigen);
        var ventas = operacionesHoy.Where(o => o.TipoOperacion == "Venta").ToList();
        cierre.CantidadVentas = ventas.Count;
        cierre.TotalVentasUSD = ventas.Sum(o => o.MontoTotalOrigen);
        cierre.TotalVentasARS = ventas.Sum(o => o.MontoTotalDestino);
        cierre.SaldoCajaARS = db.SaldosCuenta.Where(s => s.Moneda == "ARS" && s.Cuenta.Tipo == "Efectivo").Sum(s => s.Saldo);
        cierre.SaldoCajaUSD = db.SaldosCuenta.Where(s => s.Moneda == "USD" && s.Cuenta.Tipo == "Efectivo").Sum(s => s.Saldo);
        cierre.SaldoCajaEUR = db.SaldosCuenta.Where(s => s.Moneda == "EUR" && s.Cuenta.Tipo == "Efectivo").Sum(s => s.Saldo);
        cierre.TotalDiferencias = db.Arqueos.Where(a => a.Fecha >= hoy && a.Fecha < hoy.AddDays(1)).Sum(a => (decimal?)a.Diferencia) ?? 0;

        if (cierreExistente != null)
        {
            cierreExistente.FechaCierre = cierre.FechaCierre; cierreExistente.CantidadCompras = cierre.CantidadCompras;
            cierreExistente.TotalComprasUSD = cierre.TotalComprasUSD; cierreExistente.TotalComprasARS = cierre.TotalComprasARS;
            cierreExistente.CantidadVentas = cierre.CantidadVentas; cierreExistente.TotalVentasUSD = cierre.TotalVentasUSD;
            cierreExistente.TotalVentasARS = cierre.TotalVentasARS; cierreExistente.SaldoCajaARS = cierre.SaldoCajaARS;
            cierreExistente.SaldoCajaUSD = cierre.SaldoCajaUSD; cierreExistente.SaldoCajaEUR = cierre.SaldoCajaEUR;
            cierreExistente.TotalDiferencias = cierre.TotalDiferencias; cierreExistente.Observaciones = observaciones;
            cierre = cierreExistente;
        }
        else { db.CierresCaja.Add(cierre); }
        db.SaveChanges();
        _auditService.Registrar("CREATE", "CierreCaja", cierre.Id, datosNuevos: new { fecha = cierre.Fecha, compras = cierre.CantidadCompras, ventas = cierre.CantidadVentas });
        return CierreResult.Success(cierre);
    }

    public CierreResult CerrarDefinitivo(int cierreId)
    {
        using var db = _contextFactory.CreateDbContext();
        var cierre = db.CierresCaja.Find(cierreId);
        if (cierre == null) return CierreResult.Error("Cierre no encontrado");
        if (cierre.Cerrado) return CierreResult.Error("El cierre ya esta cerrado");
        cierre.Cerrado = true; cierre.FechaCierre = DateTime.Now;
        db.SaveChanges();
        _auditService.Registrar("CLOSE", "CierreCaja", cierre.Id, datosNuevos: new { cerradoDefinitivamente = true });
        return CierreResult.Success(cierre);
    }

    public bool HayDiaCerrado()
    {
        try { using var db = _contextFactory.CreateDbContext(); var hoy = DateTime.UtcNow.Date; return db.CierresCaja.Any(c => c.Cerrado && c.Fecha >= hoy && c.Fecha < hoy.AddDays(1)); }
        catch { return false; }
    }

    public CierreCaja? ObtenerCierreDeHoy()
    {
        try { using var db = _contextFactory.CreateDbContext(); var hoy = DateTime.UtcNow.Date; return db.CierresCaja.AsNoTracking().FirstOrDefault(c => c.Fecha >= hoy && c.Fecha < hoy.AddDays(1)); }
        catch { return null; }
    }

    public CierreCaja? ObtenerUltimoCierre()
    {
        using var db = _contextFactory.CreateDbContext();
        return db.CierresCaja.OrderByDescending(c => c.Fecha).FirstOrDefault();
    }
}
