using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
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
            _smtpSettings = smtpSettings.Value ?? new SmtpSettings();

            // Carga de respaldo desde variables de entorno de Render
            if (string.IsNullOrWhiteSpace(_smtpSettings.Host))
            {
                _smtpSettings.Host = Environment.GetEnvironmentVariable("Smtp__Host") 
                                  ?? Environment.GetEnvironmentVariable("SMTP_HOST") 
                                  ?? "smtp-relay.brevo.com";
            }

            if (string.IsNullOrWhiteSpace(_smtpSettings.User))
            {
                _smtpSettings.User = Environment.GetEnvironmentVariable("Smtp__User") 
                                  ?? Environment.GetEnvironmentVariable("SMTP_USER") 
                                  ?? "";
            }

            if (string.IsNullOrWhiteSpace(_smtpSettings.Password))
            {
                _smtpSettings.Password = Environment.GetEnvironmentVariable("Smtp__Password") 
                                      ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD") 
                                      ?? "";
            }

            if (_smtpSettings.Port == 0 || _smtpSettings.Port == 587)
            {
                var portVar = Environment.GetEnvironmentVariable("Smtp__Port") 
                           ?? Environment.GetEnvironmentVariable("SMTP_PORT");

                if (int.TryParse(portVar, out int parsedPort))
                {
                    _smtpSettings.Port = parsedPort;
                }
                else
                {
                    _smtpSettings.Port = 587;
                }
            }
        }

        public async Task EnviarComprobanteAsync(string emailDestino, string comprobanteId, decimal monto, string fecha)
        {
            if (string.IsNullOrWhiteSpace(_smtpSettings.Host) || string.IsNullOrWhiteSpace(_smtpSettings.User))
            {
                var errorMsg = $"[EMAIL CONFIG ERROR] Host o User vacíos. Host: '{_smtpSettings.Host}', User: '{_smtpSettings.User}'";
                Console.WriteLine(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            Console.WriteLine($"[EMAIL SERVICE] Enviando comprobante a '{emailDestino}' vía '{_smtpSettings.Host}:{_smtpSettings.Port}'...");

            using (var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_smtpSettings.User, _smtpSettings.Password);
                client.EnableSsl = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Timeout = 10000; // Timeout de 10 segundos máximo

                // Nota: Brevo requiere que el remitente coincida con tu email registrado o verificado en la plataforma
                var mailMessage = new MailMessage
                {
                    From = new MailAddress("no-reply@mipos.com", "Mi POS"),
                    Subject = $"Comprobante de Pago #{comprobanteId}",
                    Body = $@"
                        <!DOCTYPE html>
                        <html>
                        <head>
                            <style>
                                body {{ font-family: Arial, sans-serif; color: #333; line-height: 1.6; }}
                                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; }}
                                .header {{ background-color: #009ee3; color: white; padding: 15px; text-align: center; border-radius: 6px 6px 0 0; }}
                                .content {{ padding: 20px; }}
                                .details {{ background-color: #f9f9f9; padding: 15px; border-radius: 6px; margin-top: 15px; }}
                                .footer {{ font-size: 12px; color: #777; text-align: center; margin-top: 20px; }}
                            </style>
                        </head>
                        <body>
                            <div class='container'>
                                <div class='header'>
                                    <h2>¡Comprobante de Pago!</h2>
                                </div>
                                <div class='content'>
                                    <p>Hola,</p>
                                    <p>Tu pago ha sido procesado exitosamente. A continuación verás el detalle de la operación:</p>
                                    <div class='details'>
                                        <p><strong>N° de Comprobante:</strong> {comprobanteId}</p>
                                        <p><strong>Monto Total:</strong> ${monto:N2} ARS</p>
                                        <p><strong>Fecha y Hora:</strong> {fecha}</p>
                                        <p><strong>Estado:</strong> Aprobado</p>
                                    </div>
                                    <p>Gracias por tu compra.</p>
                                </div>
                                <div class='footer'>
                                    <p>Este es un correo automático, por favor no respondas a este mensaje.</p>
                                </div>
                            </div>
                        </body>
                        </html>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(emailDestino);

                await client.SendMailAsync(mailMessage);
                Console.WriteLine($"[EMAIL SERVICE OK] Comprobante enviado exitosamente a '{emailDestino}'.");
            }
        }
    }
}