namespace CasaCambio.Shared.Responses;

public class AuthResponse
{
    public string Token { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string Username { get; set; } = "";
    public string Rol { get; set; } = "";
}
