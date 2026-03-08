using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Shared.DTOs;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public ClientesController(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    [HttpGet]
    public IActionResult GetClientes()
    {
        using var db = _contextFactory.CreateDbContext();
        var clientes = db.Clientes.AsNoTracking().OrderBy(c => c.Nombre).ToList();
        return Ok(clientes.Select(c => new ClienteDto { Id = c.Id, Nombre = c.Nombre, Documento = c.Documento, Email = c.Email, FechaAlta = c.FechaAlta }));
    }
}
