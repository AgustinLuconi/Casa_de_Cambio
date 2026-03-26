namespace CasaCambio.Server.Services;

public interface IEmailService
{
    Task EnviarConfirmacionAsync(string email, string nombreCompleto, string token);
    Task EnviarRecuperacionAsync(string email, string nombreCompleto, string token);
}
