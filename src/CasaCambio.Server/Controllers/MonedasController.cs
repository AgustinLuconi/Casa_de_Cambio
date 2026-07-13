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
        return Ok(monedas.Select(m => new MonedaDto { Id = m.Id, Codigo = m.Codigo, Nombre = m.Nombre, Activa = m.Activa, TipoPase = m.TipoPase }));
    }

    [HttpPost]
    public IActionResult CrearMoneda([FromBody] CrearMonedaRequest req)
    {
        using var db = _contextFactory.CreateDbContext();
        var moneda = new Models.Moneda { Codigo = req.Codigo, Nombre = req.Nombre, Activa = true, TipoPase = string.IsNullOrEmpty(req.TipoPase) ? "D" : req.TipoPase };
        db.Monedas.Add(moneda);
        db.SaveChanges();
        return CreatedAtAction(nameof(GetMonedas), new { id = moneda.Id }, new MonedaDto { Id = moneda.Id, Codigo = moneda.Codigo, Nombre = moneda.Nombre, Activa = moneda.Activa, TipoPase = moneda.TipoPase });
    }

    [HttpPut("{id}")]
    public IActionResult ActualizarMoneda(int id, [FromBody] ActualizarMonedaRequest req)
    {
        using var db = _contextFactory.CreateDbContext();
        var moneda = db.Monedas.FirstOrDefault(m => m.Id == id);
        if (moneda == null)
            return NotFound($"Moneda {id} no encontrada.");
        moneda.Codigo = req.Codigo.Trim().ToUpper();
        moneda.Nombre = req.Nombre.Trim();
        moneda.Activa = req.Activa;
        moneda.TipoPase = string.IsNullOrEmpty(req.TipoPase) ? "D" : req.TipoPase;
        db.SaveChanges();
        return Ok(new MonedaDto { Id = moneda.Id, Codigo = moneda.Codigo, Nombre = moneda.Nombre, Activa = moneda.Activa, TipoPase = moneda.TipoPase });
    }

    [HttpDelete("{id}")]
    public IActionResult EliminarMoneda(int id)
    {
        using var db = _contextFactory.CreateDbContext();
        var moneda = db.Monedas.FirstOrDefault(m => m.Id == id);
        if (moneda == null)
            return NotFound($"Moneda {id} no encontrada.");
        bool tieneMovimientos = db.Movimientos.Any(m => m.Moneda == moneda.Codigo);
        if (tieneMovimientos)
            return BadRequest($"No se puede eliminar {moneda.Codigo}: tiene movimientos registrados.");
        bool tieneSaldos = db.SaldosCuenta.Any(s => s.Moneda == moneda.Codigo);
        if (tieneSaldos)
            return BadRequest($"No se puede eliminar {moneda.Codigo}: tiene saldos en cuentas.");
        db.Monedas.Remove(moneda);
        db.SaveChanges();
        return NoContent();
    }
}
