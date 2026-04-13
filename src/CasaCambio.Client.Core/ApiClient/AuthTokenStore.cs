using System;
using CasaCambio.Shared.Responses;

namespace SistemaCambio.ApiClient;

public class AuthTokenStore
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public string? Username { get; private set; }
    public string? Rol { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
    public bool IsTokenExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;
    public bool NeedsRefresh => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value.AddMinutes(-2);

    public void SetTokens(AuthResponse auth)
    {
        AccessToken = auth.Token;
        RefreshToken = auth.RefreshToken;
        ExpiresAt = auth.ExpiresAt;
        Username = auth.Username;
        Rol = auth.Rol;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        ExpiresAt = null;
        Username = null;
        Rol = null;
    }
}
