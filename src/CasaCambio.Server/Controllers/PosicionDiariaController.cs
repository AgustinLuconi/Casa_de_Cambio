using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Shared.DTOs;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/posicion-diaria")]
[Authorize]
public class PosicionDiariaController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public PosicionDiariaController(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    [HttpGet]
    public IActionResult GetPosicionDiaria([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        using var db = _contextFactory.CreateDbContext();
        var monedas = db.Monedas.Where(m => m.Activa).OrderBy(m => m.Codigo).AsNoTracking().ToList();
        var corteInicial = desde.Date;
        var corteFinal = hasta.Date.AddDays(1);

        var resultado = monedas.Select(m => new PosicionDiariaDto
        {
            Codigo = m.Codigo,
            Nombre = m.Nombre,
            TipoPase = m.TipoPase,
            CapInicial = SumaMovimientosEfectivo(db, m.Codigo, corteInicial),
            CapFinal = SumaMovimientosEfectivo(db, m.Codigo, corteFinal)
        }).ToList();

        return Ok(resultado);
    }

    private static decimal SumaMovimientosEfectivo(AppDbContext db, string moneda, DateTime corte)
    {
        return db.Movimientos
            .Where(mv => mv.Moneda == moneda && mv.Fecha < corte && mv.Cuenta.Tipo == "Efectivo")
            .Sum(mv => (decimal?)mv.Monto) ?? 0;
    }
}
