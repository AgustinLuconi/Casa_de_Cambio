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
        var hace30Dias = hoy.AddDays(-30);
        var hace6Meses = hoy.AddMonths(-6);

        var operacionesHoy = db.Operaciones.Where(o => o.Fecha >= hoy && o.Fecha < manana).AsNoTracking().ToList();
        var compras = operacionesHoy.Where(o => o.TipoOperacion == "Compra").ToList();
        var ventas  = operacionesHoy.Where(o => o.TipoOperacion == "Venta").ToList();
        var saldos  = db.SaldosCuenta.Include(s => s.Cuenta).Where(s => s.Cuenta.Tipo == "Efectivo").AsNoTracking().ToList();
        var cotizaciones = db.CotizacionesDiarias.Include(c => c.Moneda).Where(c => c.Fecha.Date == hoy).AsNoTracking().ToList();

        var opsPorDia = db.Operaciones
            .Where(o => o.Fecha >= hace30Dias)
            .AsNoTracking()
            .ToList()
            .GroupBy(o => o.Fecha.Date)
            .Select(g => new OperacionPorDiaDto
            {
                Fecha           = g.Key,
                CantidadCompras = g.Count(o => o.TipoOperacion == "Compra"),
                CantidadVentas  = g.Count(o => o.TipoOperacion == "Venta")
            })
            .OrderBy(x => x.Fecha)
            .ToList();

        var compMensual = db.Operaciones
            .Where(o => o.Fecha >= hace6Meses)
            .AsNoTracking()
            .ToList()
            .GroupBy(o => new { o.Fecha.Year, o.Fecha.Month })
            .Select(g => new ComparativoMensualDto
            {
                Anio              = g.Key.Year,
                Mes               = g.Key.Month,
                VolumenComprasARS = g.Where(o => o.TipoOperacion == "Compra").Sum(o => o.MontoTotalOrigen),
                VolumenVentasARS  = g.Where(o => o.TipoOperacion == "Venta").Sum(o => o.MontoTotalDestino)
            })
            .OrderBy(x => x.Anio).ThenBy(x => x.Mes)
            .ToList();

        var distMonedas = db.Operaciones
            .Include(o => o.Movimientos)
            .Where(o => o.Fecha >= hace30Dias)
            .AsNoTracking()
            .ToList()
            .SelectMany(o => o.Movimientos)
            .Where(m => m.Monto > 0)
            .GroupBy(m => m.Moneda)
            .Select(g => new OperacionPorMonedaDto
            {
                Moneda              = g.Key,
                CantidadOperaciones = g.Select(m => m.OperacionId).Distinct().Count(),
                VolumenTotal        = g.Sum(m => m.Monto)
            })
            .OrderByDescending(x => x.VolumenTotal)
            .ToList();

        return Ok(new DashboardDto
        {
            TotalOperacionesHoy = operacionesHoy.Count,
            TotalComprasHoy     = compras.Count,
            TotalVentasHoy      = ventas.Count,
            VolumenComprasARS   = compras.Sum(o => o.MontoTotalOrigen),
            VolumenVentasARS    = ventas.Sum(o => o.MontoTotalDestino),
            SaldosCaja          = saldos.Select(s => new SaldoCuentaDto { Moneda = s.Moneda, Saldo = s.Saldo }).ToList(),
            CotizacionesHoy     = cotizaciones.Select(c => new CotizacionDto { Id = c.Id, CodigoMoneda = c.Moneda.Codigo, Fecha = c.Fecha, CotizacionCompra = c.CotizacionCompra, CotizacionVenta = c.CotizacionVenta }).ToList(),
            OperacionesPorDia   = opsPorDia,
            ComparativoMensual  = compMensual,
            DistribucionMonedas = distMonedas
        });
    }
}
