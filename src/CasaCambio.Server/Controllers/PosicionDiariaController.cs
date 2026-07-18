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

        // Una sola consulta agrupada por moneda en vez de dos consultas por moneda (N+1).
        var posicionesPorMoneda = db.Movimientos
            .Where(mv => mv.Cuenta.Tipo == "Efectivo" && mv.Fecha < corteFinal)
            .GroupBy(mv => mv.Moneda)
            .Select(g => new
            {
                Moneda = g.Key,
                CapInicial = g.Sum(mv => mv.Fecha < corteInicial ? mv.Monto : 0m),
                CapFinal = g.Sum(mv => mv.Monto)
            })
            .ToDictionary(x => x.Moneda);

        var resultado = monedas.Select(m => new PosicionDiariaDto
        {
            Codigo = m.Codigo,
            Nombre = m.Nombre,
            TipoPase = m.TipoPase,
            CapInicial = posicionesPorMoneda.TryGetValue(m.Codigo, out var pos) ? pos.CapInicial : 0,
            CapFinal = posicionesPorMoneda.TryGetValue(m.Codigo, out var pos2) ? pos2.CapFinal : 0
        }).ToList();

        return Ok(resultado);
    }
}
