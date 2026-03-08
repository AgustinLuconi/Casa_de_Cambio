using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Server.Models;

namespace CasaCambio.Server.Services;

public class PPPService : IPPPService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public PPPService(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    public void RegistrarCompra(string codigoMoneda, decimal cantidadDivisa, decimal costoEnPesos)
    {
        using var db = _contextFactory.CreateDbContext();
        var moneda = db.Monedas.FirstOrDefault(m => m.Codigo == codigoMoneda);
        if (moneda == null) return;
        var tenencia = db.TenenciasMoneda.FirstOrDefault(t => t.MonedaId == moneda.Id);
        if (tenencia == null) { tenencia = new TenenciaMoneda { MonedaId = moneda.Id, CantidadTotal = 0, CostoTotal = 0 }; db.TenenciasMoneda.Add(tenencia); }
        tenencia.CantidadTotal += cantidadDivisa;
        tenencia.CostoTotal += costoEnPesos;
        db.SaveChanges();
    }

    public void RegistrarVenta(string codigoMoneda, decimal cantidadDivisa)
    {
        using var db = _contextFactory.CreateDbContext();
        var moneda = db.Monedas.FirstOrDefault(m => m.Codigo == codigoMoneda);
        if (moneda == null) return;
        var tenencia = db.TenenciasMoneda.FirstOrDefault(t => t.MonedaId == moneda.Id);
        if (tenencia == null || tenencia.CantidadTotal == 0) return;
        decimal ppp = tenencia.CostoTotal / tenencia.CantidadTotal;
        decimal costoVendido = cantidadDivisa * ppp;
        tenencia.CantidadTotal -= cantidadDivisa;
        tenencia.CostoTotal -= costoVendido;
        if (tenencia.CantidadTotal < 0) tenencia.CantidadTotal = 0;
        if (tenencia.CostoTotal < 0) tenencia.CostoTotal = 0;
        db.SaveChanges();
    }

    public PPPValidacion ValidarVenta(string codigoMoneda, decimal cotizacionVenta)
    {
        using var db = _contextFactory.CreateDbContext();
        var moneda = db.Monedas.FirstOrDefault(m => m.Codigo == codigoMoneda);
        if (moneda == null) return new PPPValidacion { Valido = true };
        var tenencia = db.TenenciasMoneda.FirstOrDefault(t => t.MonedaId == moneda.Id);
        if (tenencia == null || tenencia.CantidadTotal == 0) return new PPPValidacion { Valido = true, Mensaje = "Sin tenencia previa" };
        decimal ppp = tenencia.CostoTotal / tenencia.CantidadTotal;
        bool esRentable = cotizacionVenta >= ppp;
        return new PPPValidacion
        {
            Valido = true, PPP = ppp, CotizacionVenta = cotizacionVenta,
            Mensaje = esRentable ? $"Rentable. PPP: {ppp:N2}, Ganancia: {(cotizacionVenta - ppp):N2}/unidad" : $"ALERTA: Vendiendo por debajo del costo. PPP: {ppp:N2}, Perdida: {(ppp - cotizacionVenta):N2}/unidad"
        };
    }

    public decimal ObtenerPPP(string codigoMoneda)
    {
        using var db = _contextFactory.CreateDbContext();
        var moneda = db.Monedas.FirstOrDefault(m => m.Codigo == codigoMoneda);
        if (moneda == null) return 0;
        var tenencia = db.TenenciasMoneda.FirstOrDefault(t => t.MonedaId == moneda.Id);
        if (tenencia == null || tenencia.CantidadTotal == 0) return 0;
        return tenencia.CostoTotal / tenencia.CantidadTotal;
    }
}
