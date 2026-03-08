using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MonedasController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public MonedasController(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    [HttpGet]
    public IActionResult GetMonedas()
    {
        using var db = _contextFactory.CreateDbContext();
        var monedas = db.Monedas.Where(m => m.Activa).AsNoTracking().ToList();
        return Ok(monedas.Select(m => new MonedaDto { Id = m.Id, Codigo = m.Codigo, Nombre = m.Nombre, Activa = m.Activa }));
    }

    [HttpPost]
    public IActionResult CrearMoneda([FromBody] CrearMonedaRequest req)
    {
        using var db = _contextFactory.CreateDbContext();
        var moneda = new Models.Moneda { Codigo = req.Codigo, Nombre = req.Nombre, Activa = true };
        db.Monedas.Add(moneda);
        db.SaveChanges();
        return CreatedAtAction(nameof(GetMonedas), new { id = moneda.Id }, new MonedaDto { Id = moneda.Id, Codigo = moneda.Codigo, Nombre = moneda.Nombre, Activa = moneda.Activa });
    }
}
