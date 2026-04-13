using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Auth;
using CasaCambio.Server.Data;
using CasaCambio.Server.Services;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.Responses;

namespace CasaCambio.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly JwtService _jwtService;
    private readonly IEmailService _emailService;

    public AuthController(IDbContextFactory<AppDbContext> contextFactory, JwtService jwtService, IEmailService emailService)
    {
        _contextFactory = contextFactory;
        _jwtService = jwtService;
        _emailService = emailService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.FirstOrDefault(u => u.Username == request.Username || u.Email == request.Username);

        if (usuario != null && !usuario.Activo && !usuario.EmailConfirmado)
            return Unauthorized(new ApiErrorResponse { Code = 401, Message = "Deb\u00e9s confirmar tu email antes de iniciar sesi\u00f3n." });

        if (usuario == null || !usuario.Activo || !BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
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
    public async Task<IActionResult> Health()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var canConnect = await db.Database.CanConnectAsync();
        return Ok(new { status = canConnect ? "healthy" : "degraded", timestamp = DateTime.UtcNow });
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreCompleto) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Nombre completo y email son obligatorios." });
        if (request.Password != request.ConfirmarPassword)
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Las contrase\u00f1as no coinciden." });

        using var db = _contextFactory.CreateDbContext();
        if (db.Usuarios.Any(u => u.Email == request.Email))
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Ya existe una cuenta con ese email." });

        var confirmToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var usuario = new CasaCambio.Server.Models.Usuario
        {
            Username = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            NombreCompleto = request.NombreCompleto,
            Email = request.Email,
            Rol = "Cajero",
            Activo = false,
            EmailConfirmado = false,
            TokenConfirmacion = confirmToken,
            FechaCreacion = DateTime.UtcNow
        };
        db.Usuarios.Add(usuario);
        db.SaveChanges();

        _ = Task.Run(async () =>
        {
            try { await _emailService.EnviarConfirmacionAsync(request.Email, request.NombreCompleto, confirmToken); }
            catch { /* no bloquear el registro si el envio de email falla */ }
        });

        return Ok(new RegisterResponse
        {
            Exitoso = true,
            Mensaje = "Cuenta creada. Revis\u00e1 tu email para confirmar tu cuenta antes de iniciar sesi\u00f3n."
        });
    }

    [HttpGet("confirmar")]
    public IActionResult Confirmar([FromQuery] string token)
    {
        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.FirstOrDefault(u => u.TokenConfirmacion == token);
        if (usuario == null)
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Token inv\u00e1lido o ya utilizado." });

        usuario.Activo = true;
        usuario.EmailConfirmado = true;
        usuario.TokenConfirmacion = null;
        db.SaveChanges();

        return Ok(new RegisterResponse { Exitoso = true, Mensaje = "Email confirmado. Ya pod\u00e9s iniciar sesi\u00f3n." });
    }

    [HttpPost("recuperar")]
    public IActionResult Recuperar([FromBody] RecuperarPasswordRequest request)
    {
        const string mensajeGenerico = "Si el email existe, recibir\u00e1s instrucciones para recuperar tu contrase\u00f1a.";

        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.FirstOrDefault(u => u.Email == request.Email && u.Activo);
        if (usuario != null)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = RandomNumberGenerator.GetBytes(8);
            var recoveryToken = new string(random.Select(b => chars[b % chars.Length]).ToArray());
            usuario.TokenRecuperacion = recoveryToken;
            usuario.TokenExpiracion = DateTime.UtcNow.AddHours(1);
            db.SaveChanges();
            _ = Task.Run(async () =>
            {
                try { await _emailService.EnviarRecuperacionAsync(usuario.Email, usuario.NombreCompleto, recoveryToken); }
                catch { }
            });
        }

        return Ok(new RegisterResponse { Exitoso = true, Mensaje = mensajeGenerico });
    }

    [HttpPost("resetear")]
    public IActionResult Resetear([FromBody] ResetearPasswordRequest request)
    {
        if (request.NuevaPassword != request.ConfirmarPassword)
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Las contrase\u00f1as no coinciden." });
        if (request.NuevaPassword.Length < 8)
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "La contrase\u00f1a debe tener al menos 8 caracteres." });

        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.FirstOrDefault(u => u.TokenRecuperacion == request.Token);
        if (usuario == null || usuario.TokenExpiracion == null || usuario.TokenExpiracion < DateTime.UtcNow)
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Token inv\u00e1lido o expirado." });

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NuevaPassword);
        usuario.TokenRecuperacion = null;
        usuario.TokenExpiracion = null;
        db.SaveChanges();

        return Ok(new RegisterResponse { Exitoso = true, Mensaje = "Contrase\u00f1a actualizada. Ya pod\u00e9s iniciar sesi\u00f3n." });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = ObtenerUserIdDelToken();
        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.Find(userId);
        if (usuario == null || !usuario.Activo)
            return NotFound(new ApiErrorResponse { Code = 404, Message = "Usuario no encontrado." });

        return Ok(new UsuarioPerfilDto
        {
            Id = usuario.Id,
            Username = usuario.Username,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email,
            Rol = usuario.Rol,
            EmailConfirmado = usuario.EmailConfirmado,
            FechaCreacion = usuario.FechaCreacion
        });
    }

    [Authorize]
    [HttpPost("reenviar-confirmacion")]
    public IActionResult ReenviarConfirmacion()
    {
        var userId = ObtenerUserIdDelToken();
        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.Find(userId);
        if (usuario == null || !usuario.Activo)
            return NotFound(new ApiErrorResponse { Code = 404, Message = "Usuario no encontrado." });

        if (usuario.EmailConfirmado)
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Tu email ya est\u00e1 confirmado." });

        var confirmToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        usuario.TokenConfirmacion = confirmToken;
        db.SaveChanges();

        _ = Task.Run(async () =>
        {
            try { await _emailService.EnviarConfirmacionAsync(usuario.Email, usuario.NombreCompleto, confirmToken); }
            catch { /* falla silenciosa */ }
        });

        return Ok(new RegisterResponse
        {
            Exitoso = true,
            Mensaje = "Email de confirmaci\u00f3n reenviado. Revis\u00e1 tu casilla."
        });
    }

    [Authorize]
    [HttpPut("me")]
    public IActionResult ActualizarPerfil([FromBody] ActualizarPerfilRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreCompleto) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Nombre completo y email son obligatorios." });

        var userId = ObtenerUserIdDelToken();
        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.Find(userId);
        if (usuario == null || !usuario.Activo)
            return NotFound(new ApiErrorResponse { Code = 404, Message = "Usuario no encontrado." });

        var emailCambio = !string.Equals(usuario.Email, request.Email, StringComparison.OrdinalIgnoreCase);
        if (emailCambio && db.Usuarios.Any(u => u.Email == request.Email && u.Id != userId))
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Ya existe una cuenta con ese email." });

        usuario.NombreCompleto = request.NombreCompleto;

        if (emailCambio)
        {
            usuario.Email = request.Email;
            usuario.Username = request.Email;
            usuario.EmailConfirmado = false;
            var confirmToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            usuario.TokenConfirmacion = confirmToken;
            db.SaveChanges();
            _ = Task.Run(async () =>
            {
                try { await _emailService.EnviarConfirmacionAsync(request.Email, usuario.NombreCompleto, confirmToken); }
                catch { }
            });
        }
        else
        {
            db.SaveChanges();
        }

        return Ok(new UsuarioPerfilDto
        {
            Id = usuario.Id,
            Username = usuario.Username,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email,
            Rol = usuario.Rol,
            EmailConfirmado = usuario.EmailConfirmado
        });
    }

    [Authorize]
    [HttpPut("cambiar-password")]
    public IActionResult CambiarPassword([FromBody] CambiarPasswordRequest request)
    {
        if (request.NuevaPassword != request.ConfirmarPassword)
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "Las contrase\u00f1as no coinciden." });
        if (request.NuevaPassword.Length < 8)
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "La contrase\u00f1a debe tener al menos 8 caracteres." });

        var userId = ObtenerUserIdDelToken();
        using var db = _contextFactory.CreateDbContext();
        var usuario = db.Usuarios.Find(userId);
        if (usuario == null || !usuario.Activo)
            return NotFound(new ApiErrorResponse { Code = 404, Message = "Usuario no encontrado." });

        if (!BCrypt.Net.BCrypt.Verify(request.PasswordActual, usuario.PasswordHash))
            return BadRequest(new ApiErrorResponse { Code = 400, Message = "La contrase\u00f1a actual es incorrecta." });

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NuevaPassword);
        db.SaveChanges();

        return Ok(new RegisterResponse { Exitoso = true, Mensaje = "Contrase\u00f1a actualizada correctamente." });
    }

    private int ObtenerUserIdDelToken()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.Parse(claim?.Value ?? "0");
    }
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = "";
}
