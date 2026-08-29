using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace MiPos.API.Services
{
public interface IEmailService
{
Task EnviarComprobanteAsync(
string emailDestino,
string numeroComprobante,
decimal monto,
string fecha
);
}

```
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IConfiguration config,
        ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task EnviarComprobanteAsync(
        string emailDestino,
        string numeroComprobante,
        decimal monto,
        string fecha)
    {
        var host = _config["Smtp:Host"];
        var user = _config["Smtp:User"];
        var pass = _config["Smtp:Password"];

        if (!int.TryParse(_config["Smtp:Port"], out int port))
        {
            port = 587;
        }

        _logger.LogInformation(
            "[EMAIL] Intentando enviar comprobante a {Email}. SMTP Host={Host}, Port={Port}, UsuarioConfigurado={Usuario}",
            emailDestino,
            host,
            port,
            !string.IsNullOrEmpty(user)
        );

        if (string.IsNullOrEmpty(host) ||
            string.IsNullOrEmpty(user) ||
            string.IsNullOrEmpty(pass))
        {
            _logger.LogError(
                "[EMAIL ERROR] Configuración SMTP incompleta. Host={HostOk}, User={UserOk}, Password={PassOk}",
                !string.IsNullOrEmpty(host),
                !string.IsNullOrEmpty(user),
                !string.IsNullOrEmpty(pass)
            );

            throw new Exception("Configuración SMTP incompleta.");
        }

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress("Mi Comercio POS", user)
        );

        message.To.Add(
            MailboxAddress.Parse(emailDestino)
        );

        message.Subject =
            $"Comprobante de Pago #{numeroComprobante}";

        string htmlBody = $@"
            <div style='font-family:Arial,sans-serif;padding:20px;border:1px solid #ddd;max-width:400px;margin:auto;'>

                <h2 style='text-align:center;color:#2e7d32;'>
                    ¡Pago Confirmado!
                </h2>

                <hr/>

                <p>
                    <strong>Comprobante:</strong>
                    #{numeroComprobante}
                </p>

                <p>
                    <strong>Fecha:</strong>
                    {fecha}
                </p>

                <h3 style='background:#f5f5f5;padding:10px;text-align:center;'>
                    Total: ${monto:N2}
                </h3>

                <p style='text-align:center;color:#777;font-size:12px;'>
                    Gracias por utilizar Mi Comercio POS
                </p>

            </div>";

        message.Body = new TextPart(TextFormat.Html)
        {
            Text = htmlBody
        };

        using var client = new SmtpClient();

        try
        {
            _logger.LogInformation(
                "[EMAIL] Conectando a SMTP {Host}:{Port}",
                host,
                port
            );

            await client.ConnectAsync(
                host,
                port,
                SecureSocketOptions.StartTls
            );

            _logger.LogInformation(
                "[EMAIL] Autenticando usuario SMTP"
            );

            await client.AuthenticateAsync(
                user,
                pass
            );

            _logger.LogInformation(
                "[EMAIL] Enviando mensaje"
            );

            await client.SendAsync(message);

            await client.DisconnectAsync(true);

            _logger.LogInformation(
                "[EMAIL OK] Comprobante enviado correctamente a {Email}",
                emailDestino
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[EMAIL ERROR] No se pudo enviar correo a {Email}",
                emailDestino
            );

            throw;
        }
    }
}


}
