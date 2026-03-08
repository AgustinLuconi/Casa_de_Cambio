using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CasaCambio.Server.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CasaCambio.Server.Auth;

public class JwtService
{
    private readonly JwtSettings _settings;
    private readonly ConcurrentDictionary<string, (int UserId, DateTime Expiry)> _refreshTokens = new();

    public JwtService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerarToken(Usuario usuario)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Username),
            new Claim(ClaimTypes.Role, usuario.Rol),
            new Claim("nombre_completo", usuario.NombreCompleto)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerarRefreshToken(int userId)
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var token = Convert.ToBase64String(randomBytes);

        var expiry = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays);
        _refreshTokens[token] = (userId, expiry);

        return token;
    }

    public (bool Valid, int UserId) ValidarRefreshToken(string refreshToken)
    {
        if (_refreshTokens.TryGetValue(refreshToken, out var data))
        {
            if (data.Expiry > DateTime.UtcNow)
            {
                _refreshTokens.TryRemove(refreshToken, out _);
                return (true, data.UserId);
            }
            _refreshTokens.TryRemove(refreshToken, out _);
        }
        return (false, 0);
    }
}
