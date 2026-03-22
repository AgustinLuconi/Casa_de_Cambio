using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Shared.DTOs;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public DashboardController(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    [HttpGet]
    public IActionResult GetDashboard()
    {
        using var db = _contextFactory.CreateDbContext();
        var hoy = DateTime.UtcNow.Date;
        var manana = hoy.AddDays(1);
        var operacionesHoy = db.Operaciones.Where(o => o.Fecha >= hoy && o.Fecha < manana).AsNoTracking().ToList();
        var compras = operacionesHoy.Where(o => o.TipoOperacion == "Compra").ToList();
        var ventas = operacionesHoy.Where(o => o.TipoOperacion == "Venta").ToList();
        var saldos = db.SaldosCuenta.Include(s => s.Cuenta).Where(s => s.Cuenta.Tipo == "Efectivo").AsNoTracking().ToList();
        var cotizaciones = db.CotizacionesDiarias.Include(c => c.Moneda).Where(c => c.Fecha.Date == hoy).AsNoTracking().ToList();

        return Ok(new DashboardDto
        {
            TotalOperacionesHoy = operacionesHoy.Count,
            TotalComprasHoy = compras.Count,
            TotalVentasHoy = ventas.Count,
            VolumenComprasARS = compras.Sum(o => o.MontoTotalOrigen),
            VolumenVentasARS = ventas.Sum(o => o.MontoTotalDestino),
            SaldosCaja = saldos.Select(s => new SaldoCuentaDto { Moneda = s.Moneda, Saldo = s.Saldo }).ToList(),
            CotizacionesHoy = cotizaciones.Select(c => new CotizacionDto { Id = c.Id, CodigoMoneda = c.Moneda.Codigo, Fecha = c.Fecha, CotizacionCompra = c.CotizacionCompra, CotizacionVenta = c.CotizacionVenta }).ToList()
        });
    }
}
