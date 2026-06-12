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
public class CuentasController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IArqueoService _arqueoService;
    private readonly ICierreCajaService _cierreCajaService;

    public CuentasController(IDbContextFactory<AppDbContext> contextFactory, IArqueoService arqueoService, ICierreCajaService cierreCajaService)
    {
        _contextFactory = contextFactory;
        _arqueoService = arqueoService;
        _cierreCajaService = cierreCajaService;
    }

    [HttpGet]
    public IActionResult GetCuentas()
    {
        using var db = _contextFactory.CreateDbContext();
        var cuentas = db.Cuentas.Include(c => c.Saldos).AsNoTracking()
            .Where(c => c.Tipo != "Resultado" && c.Tipo != "Externo")
            .ToList();
        return Ok(cuentas.Select(c => new CuentaDto
        {
            Id = c.Id, Nombre = c.Nombre, Tipo = c.Tipo, LimiteDeuda = c.LimiteDeuda,
            Saldos = c.Saldos.Select(s => new SaldoCuentaDto
            {
                Moneda = s.Moneda, Saldo = s.Saldo, LimiteDeudaPersonalizado = s.LimiteDeuda
            }).ToList()
        }));
    }

    [HttpGet("estado-dia")]
    public IActionResult GetEstadoDia()
        => Ok(new { cerrado = _cierreCajaService.HayDiaCerrado() });

    [HttpPost]
    public IActionResult CrearCuenta([FromBody] CrearCuentaRequest req)
    {
        if (req.Tipo == "Efectivo" && req.Saldos.Count(s => s.Saldo != 0) > 1)
            return BadRequest(new { message = "Cuentas Efectivo deben tener una única moneda." });

        using var db = _contextFactory.CreateDbContext();
        var cuenta = new Models.Cuenta { Nombre = req.Nombre.Trim().ToUpperInvariant(), Tipo = req.Tipo, LimiteDeuda = req.LimiteDeuda };
        db.Cuentas.Add(cuenta);
        db.SaveChanges();

        foreach (var s in req.Saldos.Where(s => s.Saldo != 0))
        {
            var r = _arqueoService.RealizarArqueoCiego(cuenta.Id, s.Moneda, s.Saldo, "Saldo inicial al crear cuenta");
            if (!r.Exitoso) return BadRequest(new { message = r.Mensaje });
        }

        // Límites por divisa: después de los arqueos (que crean las filas de saldo
        // en su propio contexto), upsert del límite específico de cada moneda.
        ActualizarLimitesPorDivisa(db, cuenta.Id, req.Saldos);

        return CreatedAtAction(nameof(GetCuentas), new { id = cuenta.Id },
            new CuentaDto { Id = cuenta.Id, Nombre = cuenta.Nombre, Tipo = cuenta.Tipo, LimiteDeuda = cuenta.LimiteDeuda });
    }

    [HttpGet("{id}/movimientos")]
    public IActionResult GetMovimientos(int id,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200)
    {
        using var db = _contextFactory.CreateDbContext();
        IQueryable<Models.Movimiento> query = db.Movimientos.Include(m => m.Cuenta).Include(m => m.Operacion).Where(m => m.CuentaId == id);
        if (desde.HasValue) query = query.Where(m => m.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(m => m.Fecha < hasta.Value);
        var totalCount = query.Count();
        var items = query.OrderByDescending(m => m.Fecha)
            .Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToList();
        return Ok(new PaginatedResponse<MovimientoDto>
        {
            Items = items.Select(m => new MovimientoDto
            {
                Id = m.Id, OperacionId = m.OperacionId, CuentaId = m.CuentaId,
                NombreCuenta = m.Cuenta?.Nombre ?? "", Moneda = m.Moneda, Monto = m.Monto, Fecha = m.Fecha
            }).ToList(),
            TotalCount = totalCount, Page = page, PageSize = pageSize
        });
    }

    [HttpGet("{id}/saldos")]
    public IActionResult GetSaldos(int id)
    {
        using var db = _contextFactory.CreateDbContext();
        var saldos = db.SaldosCuenta.Where(s => s.CuentaId == id).AsNoTracking().ToList();
        return Ok(saldos.Select(s => new SaldoCuentaDto
        {
            Moneda = s.Moneda, Saldo = s.Saldo, LimiteDeudaPersonalizado = s.LimiteDeuda
        }));
    }

    [HttpPut("{id}")]
    public IActionResult ActualizarCuenta(int id, [FromBody] CrearCuentaRequest req)
    {
        if (req.Tipo == "Efectivo" && req.Saldos.Count(s => s.Saldo != 0) > 1)
            return BadRequest(new { message = "Cuentas Efectivo deben tener una única moneda." });

        using var db = _contextFactory.CreateDbContext();
        var cuenta = db.Cuentas.Find(id);
        if (cuenta == null) return NotFound();
        cuenta.Nombre = req.Nombre.Trim().ToUpperInvariant();
        cuenta.Tipo = req.Tipo;
        // El límite escalar es legacy: los clientes nuevos envían null y los límites
        // van por divisa (Saldos[].LimiteDeudaPersonalizado). Solo se pisa si viene valor.
        if (req.LimiteDeuda.HasValue)
            cuenta.LimiteDeuda = req.LimiteDeuda;
        db.SaveChanges();

        var saldosActuales = db.SaldosCuenta.Where(s => s.CuentaId == id).AsNoTracking()
            .ToDictionary(s => s.Moneda, s => s.Saldo);
        foreach (var s in req.Saldos)
        {
            var actual = saldosActuales.TryGetValue(s.Moneda, out var v) ? v : 0m;
            if (s.Saldo != actual)
            {
                var r = _arqueoService.RealizarArqueoCiego(id, s.Moneda, s.Saldo, "Ajuste manual desde edición de cuenta");
                if (!r.Exitoso) return BadRequest(new { message = r.Mensaje });
            }
        }

        ActualizarLimitesPorDivisa(db, id, req.Saldos);

        return Ok(new CuentaDto { Id = cuenta.Id, Nombre = cuenta.Nombre, Tipo = cuenta.Tipo, LimiteDeuda = cuenta.LimiteDeuda });
    }

    /// <summary>
    /// Upsert del límite de deuda específico por divisa sobre saldos_cuenta.
    /// Si la fila de saldo no existe y hay límite > 0, se crea con saldo 0
    /// (el límite es independiente de que la cuenta tenga saldo en esa moneda).
    /// </summary>
    private static void ActualizarLimitesPorDivisa(AppDbContext db, int cuentaId, IEnumerable<SaldoCuentaDto> saldos)
    {
        foreach (var s in saldos)
        {
            var fila = db.SaldosCuenta.FirstOrDefault(x => x.CuentaId == cuentaId && x.Moneda == s.Moneda);
            if (fila != null)
            {
                if (fila.LimiteDeuda != s.LimiteDeudaPersonalizado)
                    fila.LimiteDeuda = s.LimiteDeudaPersonalizado;
            }
            else if (s.LimiteDeudaPersonalizado > 0)
            {
                db.SaldosCuenta.Add(new Models.SaldoCuenta
                {
                    CuentaId = cuentaId, Moneda = s.Moneda, Saldo = 0,
                    LimiteDeuda = s.LimiteDeudaPersonalizado
                });
            }
        }
        db.SaveChanges();
    }

    [HttpDelete("{id}")]
    public IActionResult EliminarCuenta(int id)
    {
        using var db = _contextFactory.CreateDbContext();
        var cuenta = db.Cuentas.Include(c => c.Saldos).FirstOrDefault(c => c.Id == id);
        if (cuenta == null)
            return NotFound($"Cuenta {id} no encontrada.");
        bool tieneMovimientos = db.Movimientos.Any(m => m.CuentaId == id);
        if (tieneMovimientos)
            return BadRequest("No se puede eliminar una cuenta con movimientos registrados.");

        db.SaldosCuenta.RemoveRange(cuenta.Saldos);
        db.Cuentas.Remove(cuenta);
        db.SaveChanges();
        return NoContent();
    }
}
