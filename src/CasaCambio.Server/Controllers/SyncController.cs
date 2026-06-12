using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Server.Services;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.Responses;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly IOperacionService _operacionService;
    private readonly IPPPService _pppService;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SyncController(IOperacionService operacionService, IPPPService pppService, IDbContextFactory<AppDbContext> contextFactory)
    {
        _operacionService = operacionService;
        _pppService = pppService;
        _contextFactory = contextFactory;
    }

    [HttpPost("push")]
    public IActionResult Push([FromBody] SyncPushRequest request)
    {
        var resultados = new List<SyncResultItem>();
        foreach (var op in request.Operaciones.OrderBy(o => o.FechaCreacionLocal))
        {
            try
            {
                OperacionResult result;
                if (op.TipoOperacion == "Compra" || op.TipoOperacion == "Venta")
                {
                    result = _operacionService.GuardarOperacion(op.TipoOperacion, op.CuentaOrigenId, op.CuentaDestinoId, op.MonedaOrigen, op.MonedaDestino, op.MontoOrigen, op.MontoDestino, op.CotizacionAplicada, op.ClienteId, op.Observaciones, op.LocalId);
                    if (result.Exitoso)
                    {
                        if (op.TipoOperacion == "Compra") _pppService.RegistrarCompra(op.MonedaDestino, op.MontoDestino, op.MontoOrigen);
                        else _pppService.RegistrarVenta(op.MonedaOrigen, op.MontoOrigen);
                    }
                }
                else
                {
                    result = _operacionService.GuardarCreditoDebito(op.CuentaDestinoId, op.CuentaOrigenId, op.MonedaDestino, op.MonedaOrigen, op.MontoDestino, op.MontoOrigen, op.CotizacionAplicada, op.ClienteId, op.Observaciones, op.LocalId);
                }
                resultados.Add(new SyncResultItem { LocalId = op.LocalId, ServerOperacionId = result.OperacionId, Exitoso = result.Exitoso, Mensaje = result.Exitoso ? null : result.Mensaje });
            }
            catch (Exception ex)
            {
                resultados.Add(new SyncResultItem { LocalId = op.LocalId, Exitoso = false, Mensaje = ex.Message });
            }
        }
        var statusCode = resultados.All(r => r.Exitoso) ? 200 : 207;
        return StatusCode(statusCode, new SyncPushResponse { Resultados = resultados });
    }

    [HttpGet("pull")]
    public IActionResult Pull()
    {
        using var db = _contextFactory.CreateDbContext();
        var cuentas = db.Cuentas.Include(c => c.Saldos).AsNoTracking().ToList();
        var monedas = db.Monedas.Where(m => m.Activa).AsNoTracking().ToList();
        var hoy = DateTime.UtcNow.Date;
        var cotizaciones = db.CotizacionesDiarias.Include(c => c.Moneda).Where(c => c.Fecha.Date == hoy).AsNoTracking().ToList();

        return Ok(new
        {
            cuentas = cuentas.Select(c => new CuentaDto { Id = c.Id, Nombre = c.Nombre, Tipo = c.Tipo, Saldos = c.Saldos.Select(s => new SaldoCuentaDto { Moneda = s.Moneda, Saldo = s.Saldo }).ToList() }),
            monedas = monedas.Select(m => new MonedaDto { Id = m.Id, Codigo = m.Codigo, Nombre = m.Nombre, Activa = m.Activa }),
            cotizaciones = cotizaciones.Select(c => new CotizacionDto { Id = c.Id, CodigoMoneda = c.Moneda.Codigo, Fecha = c.Fecha, CotizacionCompra = c.CotizacionCompra, CotizacionVenta = c.CotizacionVenta })
        });
    }
}
