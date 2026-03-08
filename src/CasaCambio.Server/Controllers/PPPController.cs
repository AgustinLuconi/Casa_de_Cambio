using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CasaCambio.Server.Services;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PPPController : ControllerBase
{
    private readonly IPPPService _pppService;
    public PPPController(IPPPService pppService) { _pppService = pppService; }

    [HttpGet("{moneda}")]
    public IActionResult GetPPP(string moneda)
    {
        var ppp = _pppService.ObtenerPPP(moneda);
        return Ok(new { moneda, ppp });
    }

    [HttpGet("{moneda}/validar-venta")]
    public IActionResult ValidarVenta(string moneda, [FromQuery] decimal cotizacion)
    {
        var result = _pppService.ValidarVenta(moneda, cotizacion);
        return Ok(result);
    }
}
