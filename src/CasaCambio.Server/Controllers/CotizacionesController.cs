using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Server.Services;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CotizacionesController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;

    public CotizacionesController(IDbContextFactory<AppDbContext> contextFactory, IAuditService auditService)
    { _contextFactory = contextFactory; _auditService = auditService; }

    [HttpGet("hoy")]
    public IActionResult GetCotizacionesHoy()
    {
        using var db = _contextFactory.CreateDbContext();
        var hoy = DateTime.UtcNow.Date;
        var cotizaciones = db.CotizacionesDiarias
            .Include(c => c.Moneda)
            .Where(c => c.Fecha.Date == hoy)
            .AsNoTracking().ToList();
        return Ok(cotizaciones.Select(c => new CotizacionDto
        {
            Id = c.Id, CodigoMoneda = c.Moneda.Codigo, Fecha = c.Fecha,
            CotizacionCompra = c.CotizacionCompra, CotizacionVenta = c.CotizacionVenta
        }));
    }

    [HttpPost]
    public IActionResult GuardarCotizacion([FromBody] CrearCotizacionRequest req)
    {
        using var db = _contextFactory.CreateDbContext();
        var moneda = db.Monedas.FirstOrDefault(m => m.Codigo == req.CodigoMoneda);
        if (moneda == null) return BadRequest(new { message = "Moneda no encontrada" });

        var hoy = DateTime.UtcNow.Date;
        var existente = db.CotizacionesDiarias.FirstOrDefault(c => c.MonedaId == moneda.Id && c.Fecha.Date == hoy);

        if (existente != null)
        {
            var anterior = existente.CotizacionVenta;
            existente.CotizacionCompra = req.CotizacionCompra;
            existente.CotizacionVenta = req.CotizacionVenta;
            _auditService.RegistrarCambioCotizacion(moneda.Id, anterior, req.CotizacionVenta);
        }
        else
        {
            db.CotizacionesDiarias.Add(new Models.CotizacionDiaria
            {
                MonedaId = moneda.Id, Fecha = hoy,
                CotizacionCompra = req.CotizacionCompra, CotizacionVenta = req.CotizacionVenta
            });
        }
        db.SaveChanges();
        return Ok(new { message = "Cotizacion guardada" });
    }
}
