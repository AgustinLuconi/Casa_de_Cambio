using Resend;

namespace CasaCambio.Server.Services;

public class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly string _from;

    public EmailService(IResend resend, IConfiguration config)
    {
        _resend = resend;
        _from = config["Email:FromAddress"] ?? "noreply@casacambio.app";
    }

    public async Task EnviarConfirmacionAsync(string email, string nombreCompleto, string token)
    {
        var link = $"https://casa-cambio-api.fly.dev/api/auth/confirmar?token={token}";
        var message = new EmailMessage();
        message.From = _from;
        message.To.Add(email);
        message.Subject = "Confirmá tu cuenta - Casa de Cambio";
        message.HtmlBody =
            "<h2>Confirmación de cuenta</h2>" +
            $"<p>Hola {nombreCompleto},</p>" +
            "<p>Para confirmar tu cuenta hacé clic en el siguiente enlace:</p>" +
            $"<p><a href=\"{link}\">{link}</a></p>" +
            "<p>El enlace expira en 24 horas.</p>";
        await _resend.EmailSendAsync(message);
    }

    public async Task EnviarRecuperacionAsync(string email, string nombreCompleto, string token)
    {
        var message = new EmailMessage();
        message.From = _from;
        message.To.Add(email);
        message.Subject = "Recuperación de contraseña — Casa de Cambio";
        message.HtmlBody =
            "<div style='font-family:Arial,sans-serif;max-width:480px;margin:0 auto;background:#101c22;color:#e2e8f0;padding:32px;border-radius:12px'>" +
            "<h2 style='color:white;margin-top:0'>Recuperación de contraseña</h2>" +
            $"<p>Hola <strong>{nombreCompleto}</strong>,</p>" +
            "<p>Recibimos una solicitud para recuperar tu contraseña.</p>" +
            "<p>Tu código de recuperación es:</p>" +
            "<div style='background:#0c151a;border:1px solid #1e3441;border-radius:10px;padding:20px;text-align:center;margin:20px 0'>" +
            $"<span style='font-family:monospace;font-size:32px;font-weight:bold;letter-spacing:8px;color:#13a4ec'>{token}</span>" +
            "</div>" +
            "<p style='font-size:13px;color:#94a3b8'>Ingresá este código en la app:<br>" +
            "<strong>Login → '¿Olvidaste tu contraseña?' → 'Ya tengo el código'</strong></p>" +
            "<p style='font-size:13px;color:#94a3b8'>Este código expira en <strong>1 hora</strong>.</p>" +
            "<p style='font-size:12px;color:#64748b'>Si no solicitaste esto, ignorá este email.</p>" +
            "</div>";
        await _resend.EmailSendAsync(message);
    }
}
