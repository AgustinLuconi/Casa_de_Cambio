namespace CasaCambio.Server.Auth;

public class JwtSettings
{
    public string SecretKey { get; set; } = "";
    public string Issuer { get; set; } = "CasaCambio.Server";
    public string Audience { get; set; } = "CasaCambio.Desktop";
    public int AccessTokenExpirationMinutes { get; set; } = 30;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
