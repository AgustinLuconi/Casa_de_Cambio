using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CasaCambio.Server.Services;
using CasaCambio.Shared.DTOs;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/cierre")]
[Authorize]
public class CierreCajaController : ControllerBase
{
    private readonly ICierreCajaService _cierreCajaService;
    public CierreCajaController(ICierreCajaService cierreCajaService) { _cierreCajaService = cierreCajaService; }

    [HttpGet("hoy")]
    public IActionResult GetCierreHoy()
    {
        var cierre = _cierreCajaService.ObtenerCierreDeHoy();
        if (cierre == null) return NotFound(new { message = "No hay cierre para hoy" });
        return Ok(MapCierre(cierre));
    }

    [HttpPost("generar")]
    public IActionResult GenerarCierre([FromBody] GenerarCierreRequest? req)
    {
        var result = _cierreCajaService.GenerarCierre(req?.Observaciones ?? "");
        if (!result.Exitoso) return BadRequest(new { message = result.Mensaje });
        return Ok(MapCierre(result.Cierre!));
    }

    [HttpPost("{id}/cerrar")]
    public IActionResult CerrarDefinitivo(int id)
    {
        var result = _cierreCajaService.CerrarDefinitivo(id);
        if (!result.Exitoso) return BadRequest(new { message = result.Mensaje });
        return Ok(MapCierre(result.Cierre!));
    }

    private static CierreCajaDto MapCierre(Models.CierreCaja c) => new()
    {
        Id = c.Id, Fecha = c.Fecha, FechaCierre = c.FechaCierre, Usuario = c.Usuario,
        CantidadCompras = c.CantidadCompras, TotalComprasUSD = c.TotalComprasUSD, TotalComprasARS = c.TotalComprasARS,
        CantidadVentas = c.CantidadVentas, TotalVentasUSD = c.TotalVentasUSD, TotalVentasARS = c.TotalVentasARS,
        SaldoCajaARS = c.SaldoCajaARS, SaldoCajaUSD = c.SaldoCajaUSD, SaldoCajaEUR = c.SaldoCajaEUR,
        TotalDiferencias = c.TotalDiferencias, Observaciones = c.Observaciones, Cerrado = c.Cerrado
    };
}

public class GenerarCierreRequest { public string Observaciones { get; set; } = ""; }
