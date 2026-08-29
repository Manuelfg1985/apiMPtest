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

            // Respaldo directo de variables de entorno de Render
            // Si el binder automático falla o si se configuró como SMTP_HOST / Smtp_Host
            if (string.IsNullOrWhiteSpace(_smtpSettings.Host))
            {
                _smtpSettings.Host = Environment.GetEnvironmentVariable("Smtp__Host") 
                                  ?? Environment.GetEnvironmentVariable("SMTP_HOST") 
                                  ?? Environment.GetEnvironmentVariable("Smtp_Host") 
                                  ?? "";
            }

            if (string.IsNullOrWhiteSpace(_smtpSettings.User))
            {
                _smtpSettings.User = Environment.GetEnvironmentVariable("Smtp__User") 
                                  ?? Environment.GetEnvironmentVariable("SMTP_USER") 
                                  ?? Environment.GetEnvironmentVariable("Smtp_User") 
                                  ?? "";
            }

            if (string.IsNullOrWhiteSpace(_smtpSettings.Password))
            {
                _smtpSettings.Password = Environment.GetEnvironmentVariable("Smtp__Password") 
                                      ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD") 
                                      ?? Environment.GetEnvironmentVariable("Smtp_Password") 
                                      ?? "";
            }

            if (_smtpSettings.Port == 0 || _smtpSettings.Port == 587)
            {
                var portVar = Environment.GetEnvironmentVariable("Smtp__Port") 
                           ?? Environment.GetEnvironmentVariable("SMTP_PORT") 
                           ?? Environment.GetEnvironmentVariable("Smtp_Port");

                if (int.TryParse(portVar, out int parsedPort))
                {
                    _smtpSettings.Port = parsedPort;
                }
                else
                {
                    _smtpSettings.Port = 587; // Puerto por defecto en Render (STARTTLS)
                }
            }
        }

        public async Task EnviarComprobanteAsync(string emailDestino, string comprobanteId, decimal monto, string fecha)
        {
            // Validar existencia de credenciales
            if (string.IsNullOrWhiteSpace(_smtpSettings.Host) || string.IsNullOrWhiteSpace(_smtpSettings.User))
            {
                var errorMsg = $"[EMAIL CONFIG ERROR] Host o User vacíos en Render. Host: '{_smtpSettings.Host}', User: '{_smtpSettings.User}'";
                Console.WriteLine(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            Console.WriteLine($"[EMAIL SERVICE] Intentando enviar correo a '{emailDestino}' mediante '{_smtpSettings.Host}:{_smtpSettings.Port}' (User: '{_smtpSettings.User}')...");

            using (var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_smtpSettings.User, _smtpSettings.Password);
                client.EnableSsl = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Timeout = 10000; // 10 segundos máximo de espera para evitar cuelgues

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpSettings.User, "Mi POS"),
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
                                    <p>Tu pago ha sido procesado exitosamente. A continuación verás el resumen de la operación:</p>
                                    
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
                Console.WriteLine($"[EMAIL SERVICE OK] Correo enviado exitosamente a '{emailDestino}'.");
            }
        }
    }
}