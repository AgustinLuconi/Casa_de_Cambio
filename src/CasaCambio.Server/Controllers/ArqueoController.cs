using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CasaCambio.Server.Services;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.DTOs;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ArqueoController : ControllerBase
{
    private readonly IArqueoService _arqueoService;
    public ArqueoController(IArqueoService arqueoService) { _arqueoService = arqueoService; }

    [HttpPost]
    public IActionResult RealizarArqueo([FromBody] CrearArqueoRequest req)
    {
        var result = _arqueoService.RealizarArqueoCiego(req.CuentaId, req.Moneda, req.SaldoArqueo, req.Observaciones);
        if (result.Exitoso)
            return Ok(new ArqueoDto { Id = result.ArqueoId!.Value, Diferencia = result.Diferencia, CuentaId = req.CuentaId });
        return BadRequest(new { message = result.Mensaje });
    }
}
