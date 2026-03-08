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
public class CuentasController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public CuentasController(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    [HttpGet]
    public IActionResult GetCuentas()
    {
        using var db = _contextFactory.CreateDbContext();
        var cuentas = db.Cuentas.Include(c => c.Saldos).AsNoTracking().ToList();
        return Ok(cuentas.Select(c => new CuentaDto
        {
            Id = c.Id, Nombre = c.Nombre, Tipo = c.Tipo,
            Saldos = c.Saldos.Select(s => new SaldoCuentaDto { Moneda = s.Moneda, Saldo = s.Saldo }).ToList()
        }));
    }

    [HttpPost]
    public IActionResult CrearCuenta([FromBody] CrearCuentaRequest req)
    {
        using var db = _contextFactory.CreateDbContext();
        var cuenta = new Models.Cuenta { Nombre = req.Nombre, Tipo = req.Tipo };
        db.Cuentas.Add(cuenta);
        db.SaveChanges();
        return CreatedAtAction(nameof(GetCuentas), new { id = cuenta.Id }, new CuentaDto { Id = cuenta.Id, Nombre = cuenta.Nombre, Tipo = cuenta.Tipo });
    }

    [HttpGet("{id}/movimientos")]
    public IActionResult GetMovimientos(int id, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        using var db = _contextFactory.CreateDbContext();
        IQueryable<Models.Movimiento> query = db.Movimientos.Include(m => m.Cuenta).Include(m => m.Operacion).Where(m => m.CuentaId == id);
        if (desde.HasValue) query = query.Where(m => m.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(m => m.Fecha < hasta.Value);
        var movimientos = query.OrderByDescending(m => m.Fecha).Take(500).AsNoTracking().ToList();
        return Ok(movimientos.Select(m => new MovimientoDto
        {
            Id = m.Id, OperacionId = m.OperacionId, CuentaId = m.CuentaId,
            NombreCuenta = m.Cuenta?.Nombre ?? "", Moneda = m.Moneda, Monto = m.Monto, Fecha = m.Fecha
        }));
    }

    [HttpGet("{id}/saldos")]
    public IActionResult GetSaldos(int id)
    {
        using var db = _contextFactory.CreateDbContext();
        var saldos = db.SaldosCuenta.Where(s => s.CuentaId == id).AsNoTracking().ToList();
        return Ok(saldos.Select(s => new SaldoCuentaDto { Moneda = s.Moneda, Saldo = s.Saldo }));
    }
}
