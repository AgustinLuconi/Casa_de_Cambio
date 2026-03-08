using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Auth;
using CasaCambio.Server.Data;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.Responses;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly JwtService _jwtService;

    public AuthController(IDbContextFactory<AppDbContext> contextFactory, JwtService jwtService)
    {
        _contextFactory = contextFactory;
        _jwtService = jwtService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.FirstOrDefault(u => u.Username == request.Username && u.Activo);
        if (usuario == null || !BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
            return Unauthorized(new ApiErrorResponse { Code = 401, Message = "Credenciales invalidas" });

        var token = _jwtService.GenerarToken(usuario);
        var refreshToken = _jwtService.GenerarRefreshToken(usuario.Id);

        return Ok(new AuthResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            Username = usuario.Username,
            Rol = usuario.Rol
        });
    }

    [HttpPost("refresh")]
    public IActionResult Refresh([FromBody] RefreshRequest request)
    {
        var (valid, userId) = _jwtService.ValidarRefreshToken(request.RefreshToken);
        if (!valid)
            return Unauthorized(new ApiErrorResponse { Code = 401, Message = "Refresh token invalido o expirado" });

        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.Find(userId);
        if (usuario == null || !usuario.Activo)
            return Unauthorized(new ApiErrorResponse { Code = 401, Message = "Usuario no encontrado o inactivo" });

        var token = _jwtService.GenerarToken(usuario);
        var newRefreshToken = _jwtService.GenerarRefreshToken(usuario.Id);

        return Ok(new AuthResponse
        {
            Token = token,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            Username = usuario.Username,
            Rol = usuario.Rol
        });
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = "";
}
