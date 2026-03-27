using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Data;
using CasaCambio.Server.Models;
using CasaCambio.Shared.Requests;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfiguracionController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    public ConfiguracionController(IDbContextFactory<AppDbContext> contextFactory) { _contextFactory = contextFactory; }

    [HttpGet("{clave}")]
    public IActionResult ObtenerConfiguracion(string clave)
    {
        using var db = _contextFactory.CreateDbContext();
        var config = db.ConfiguracionSistema.Find(clave);
        if (config == null) return NotFound();
        return Ok(new { config.Clave, config.Valor, config.Descripcion });
    }

    [HttpPut("{clave}")]
    public IActionResult ActualizarConfiguracion(string clave, [FromBody] ActualizarConfigRequest req)
    {
        using var db = _contextFactory.CreateDbContext();
        var config = db.ConfiguracionSistema.Find(clave);
        if (config == null)
        {
            config = new ConfiguracionSistema { Clave = clave, Valor = req.Valor, Descripcion = req.Descripcion ?? "" };
            db.ConfiguracionSistema.Add(config);
        }
        else
        {
            config.Valor = req.Valor;
            if (req.Descripcion != null) config.Descripcion = req.Descripcion;
        }
        db.SaveChanges();
        return Ok(new { config.Clave, config.Valor, config.Descripcion });
    }
}
