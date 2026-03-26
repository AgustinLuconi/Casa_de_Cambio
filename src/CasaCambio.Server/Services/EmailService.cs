using System.Net;
using System.Net.Mail;

namespace CasaCambio.Server.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config) { _config = config; }

    private SmtpClient CrearSmtpClient()
    {
        var s = _config.GetSection("Email");
        return new SmtpClient
        {
            Host = s["SmtpHost"] ?? "smtp.resend.com",
            Port = int.Parse(s["SmtpPort"] ?? "465"),
            EnableSsl = bool.Parse(s["UseSsl"] ?? "true"),
            Credentials = new NetworkCredential(s["Username"] ?? "resend", s["Password"] ?? "")
        };
    }

    private MailAddress ObtenerRemitente()
    {
        var s = _config.GetSection("Email");
        return new MailAddress(s["FromAddress"] ?? "noreply@tudominio.com", s["FromName"] ?? "Treasury ERP");
    }

    public async Task EnviarConfirmacionAsync(string email, string nombreCompleto, string token)
    {
        var link = $"https://casa-cambio-api.fly.dev/api/auth/confirmar?token={token}";
        var body = "<h2>Confirmaci\u00f3n de cuenta</h2>" +
                   $"<p>Hola {nombreCompleto},</p>" +
                   $"<p>Para confirmar tu cuenta hac\u00e9 clic en el siguiente enlace:</p>" +
                   $"<p><a href=\"{link}\">{link}</a></p>" +
                   "<p>El enlace expira en 24 horas.</p>";
        await EnviarAsync(email, "Confirm\u00e1 tu cuenta - Treasury ERP", body);
    }

    public async Task EnviarRecuperacionAsync(string email, string nombreCompleto, string token)
    {
        var body =
            "<div style='font-family:Arial,sans-serif;max-width:480px;margin:0 auto;background:#101c22;color:#e2e8f0;padding:32px;border-radius:12px'>" +
            "<h2 style='color:white;margin-top:0'>Recuperaci\u00f3n de contrase\u00f1a</h2>" +
            $"<p>Hola <strong>{nombreCompleto}</strong>,</p>" +
            "<p>Recibimos una solicitud para recuperar tu contrase\u00f1a.</p>" +
            "<p>Tu c\u00f3digo de recuperaci\u00f3n es:</p>" +
            "<div style='background:#0c151a;border:1px solid #1e3441;border-radius:10px;padding:20px;text-align:center;margin:20px 0'>" +
            $"<span style='font-family:monospace;font-size:32px;font-weight:bold;letter-spacing:8px;color:#13a4ec'>{token}</span>" +
            "</div>" +
            "<p style='font-size:13px;color:#94a3b8'>Ingres\u00e1 este c\u00f3digo en la app:<br>" +
            "<strong>Login \u2192 '\u00bfOlvidaste tu contrase\u00f1a?' \u2192 'Ya tengo el c\u00f3digo'</strong></p>" +
            "<p style='font-size:13px;color:#94a3b8'>Este c\u00f3digo expira en <strong>1 hora</strong>.</p>" +
            "<p style='font-size:12px;color:#64748b'>Si no solicit\u00e1ste esto, ignor\u00e1 este email.</p>" +
            "</div>";

        await EnviarAsync(email, "Recuperaci\u00f3n de contrase\u00f1a \u2014 Treasury ERP", body);
    }

    private async Task EnviarAsync(string destinatario, string asunto, string htmlBody)
    {
        using var client = CrearSmtpClient();
        using var mensaje = new MailMessage
        {
            From = ObtenerRemitente(),
            Subject = asunto,
            Body = htmlBody,
            IsBodyHtml = true
        };
        mensaje.To.Add(destinatario);
        await client.SendMailAsync(mensaje);
    }
}
