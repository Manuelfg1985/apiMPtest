using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Microsoft.Extensions.Configuration;

namespace MiPos.API.Services
{
    public interface IEmailService
    {
        Task EnviarComprobanteAsync(string emailDestino, string numeroComprobante, decimal monto, string fecha);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task EnviarComprobanteAsync(string emailDestino, string numeroComprobante, decimal monto, string fecha)
        {
            var host = _config["Smtp:Host"];
            var user = _config["Smtp:User"];
            var pass = _config["Smtp:Password"];

            // Si los parámetros SMTP no están configurados en appsettings.json, evitamos la conexión
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Mi Comercio POS", user));
            message.To.Add(new MailboxAddress("", emailDestino));
            message.Subject = $"Comprobante de Pago #{numeroComprobante}";

            string htmlBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; max-width: 400px; margin: 0 auto;'>
                    <h2 style='text-align: center; color: #2e7d32;'>¡Pago Confirmado!</h2>
                    <hr/>
                    <p><strong>Comprobante:</strong> #{numeroComprobante}</p>
                    <p><strong>Fecha:</strong> {fecha}</p>
                    <h3 style='background-color: #f5f5f5; padding: 10px; text-align: center;'>Total: ${monto:N2}</h3>
                </div>";

            message.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

            using var client = new SmtpClient();
            if (int.TryParse(_config["Smtp:Port"], out int port))
            {
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(user, pass);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }
    }
}