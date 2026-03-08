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
public class OperacionesController : ControllerBase
{
    private readonly IOperacionService _operacionService;
    private readonly IPPPService _pppService;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public OperacionesController(IOperacionService operacionService, IPPPService pppService, IDbContextFactory<AppDbContext> contextFactory)
    {
        _operacionService = operacionService;
        _pppService = pppService;
        _contextFactory = contextFactory;
    }

    [HttpGet]
    public IActionResult GetOperaciones([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] string? tipo, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        using var db = _contextFactory.CreateDbContext();
        IQueryable<Models.Operacion> query = db.Operaciones.Include(o => o.Cliente).Include(o => o.Movimientos).ThenInclude(m => m.Cuenta);
        if (desde.HasValue) query = query.Where(o => o.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(o => o.Fecha < hasta.Value);
        if (!string.IsNullOrEmpty(tipo)) query = query.Where(o => o.TipoOperacion == tipo);
        var total = query.Count();
        var items = query.OrderByDescending(o => o.Fecha).Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToList();
        return Ok(new PaginatedResponse<OperacionDto>
        {
            Items = items.Select(MapOperacion).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}")]
    public IActionResult GetOperacion(int id)
    {
        using var db = _contextFactory.CreateDbContext();
        var op = db.Operaciones.Include(o => o.Cliente).Include(o => o.Movimientos).ThenInclude(m => m.Cuenta).AsNoTracking().FirstOrDefault(o => o.Id == id);
        if (op == null) return NotFound();
        return Ok(MapOperacion(op));
    }

    [HttpPost("compra")]
    public IActionResult Compra([FromBody] CrearOperacionRequest req)
    {
        var result = _operacionService.GuardarOperacion("Compra", req.CuentaOrigenId, req.CuentaDestinoId, req.MonedaOrigen, req.MonedaDestino, req.MontoOrigen, req.MontoDestino, req.Cotizacion, req.ClienteId, req.Observaciones);
        if (result.Exitoso) _pppService.RegistrarCompra(req.MonedaDestino, req.MontoDestino, req.MontoOrigen);
        return result.Exitoso ? Ok(OperacionResponse.Success(result.OperacionId!.Value)) : BadRequest(OperacionResponse.Error(result.Mensaje));
    }

    [HttpPost("venta")]
    public IActionResult Venta([FromBody] CrearOperacionRequest req)
    {
        var result = _operacionService.GuardarOperacion("Venta", req.CuentaOrigenId, req.CuentaDestinoId, req.MonedaOrigen, req.MonedaDestino, req.MontoOrigen, req.MontoDestino, req.Cotizacion, req.ClienteId, req.Observaciones);
        if (result.Exitoso) _pppService.RegistrarVenta(req.MonedaOrigen, req.MontoOrigen);
        return result.Exitoso ? Ok(OperacionResponse.Success(result.OperacionId!.Value)) : BadRequest(OperacionResponse.Error(result.Mensaje));
    }

    [HttpPost("credito-debito")]
    public IActionResult CreditoDebito([FromBody] CrearCreditoDebitoRequest req)
    {
        var result = _operacionService.GuardarCreditoDebito(req.CuentaCreditoId, req.CuentaDebitoId, req.MonedaCredito, req.MonedaDebito, req.MontoCredito, req.MontoDebito, req.Cotizacion, req.ClienteId, req.Observaciones);
        return result.Exitoso ? Ok(OperacionResponse.Success(result.OperacionId!.Value)) : BadRequest(OperacionResponse.Error(result.Mensaje));
    }

    [HttpPost("interbancaria")]
    public IActionResult Interbancaria([FromBody] CrearInterbancarioRequest req)
    {
        var result = _operacionService.GuardarOperacionInterbancaria("Interbancaria", req.CuentaOrigenId, req.CuentaDestinoId, req.MonedaOrigen, req.MonedaDestino, req.MontoOrigen, req.MontoDestino, req.Cotizacion, req.Observaciones);
        return result.Exitoso ? Ok(OperacionResponse.Success(result.OperacionId!.Value)) : BadRequest(OperacionResponse.Error(result.Mensaje));
    }

    private static OperacionDto MapOperacion(Models.Operacion o) => new()
    {
        Id = o.Id, Fecha = o.Fecha, TipoOperacion = o.TipoOperacion, ClienteId = o.ClienteId,
        NombreCliente = o.Cliente?.Nombre, MontoTotalOrigen = o.MontoTotalOrigen,
        MontoTotalDestino = o.MontoTotalDestino, CotizacionAplicada = o.CotizacionAplicada,
        Observaciones = o.Observaciones,
        Movimientos = o.Movimientos.Select(m => new MovimientoDto
        {
            Id = m.Id, OperacionId = m.OperacionId, CuentaId = m.CuentaId,
            NombreCuenta = m.Cuenta?.Nombre ?? "", Moneda = m.Moneda, Monto = m.Monto, Fecha = m.Fecha
        }).ToList()
    };
}
