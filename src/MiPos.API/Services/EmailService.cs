using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MiPos.API.Services
{
    public interface IEmailService
    {
        Task EnviarComprobanteAsync(string emailDestino, string comprobanteId, decimal monto, string fecha);
    }

    public class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailService(IOptions<SmtpSettings> smtpSettings)
        {
            _smtpSettings = smtpSettings.Value;
        }

        public async Task EnviarComprobanteAsync(string emailDestino, string comprobanteId, decimal monto, string fecha)
        {
            if (string.IsNullOrWhiteSpace(_smtpSettings.Host) || string.IsNullOrWhiteSpace(_smtpSettings.User))
            {
                throw new InvalidOperationException($"[EMAIL CONFIG ERROR] Host o User vacíos en Render. Host: '{_smtpSettings.Host}', User: '{_smtpSettings.User}'");
            }

            using (var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_smtpSettings.User, _smtpSettings.Password);
                client.EnableSsl = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Timeout = 10000; // 10 segundos max de timeout (evita cuelgues de 2 min)

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpSettings.User, "Mi POS"),
                    Subject = $"Comprobante de Pago #{comprobanteId}",
                    Body = $@"
                        <h2>¡Gracias por tu compra!</h2>
                        <p>Hemos procesado tu pago exitosamente.</p>
                        <ul>
                            <li><strong>Comprobante N°:</strong> {comprobanteId}</li>
                            <li><strong>Monto:</strong> ${monto:N2} ARS</li>
                            <li><strong>Fecha:</strong> {fecha}</li>
                        </ul>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(emailDestino);

                await client.SendMailAsync(mailMessage);
            }
        }
    }
}