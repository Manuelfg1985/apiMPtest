using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiPos.API.Services
{
    public interface IEmailService
    {
        Task EnviarComprobanteAsync(string emailDestino, string comprobanteId, decimal monto, string fecha);
    }

    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;

        public EmailService()
        {
            _httpClient = new HttpClient();
        }

        public async Task EnviarComprobanteAsync(string emailDestino, string comprobanteId, decimal monto, string fecha)
        {
            // Obtener la API Key de Brevo desde las variables de entorno de Render
            var apiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY") 
                        ?? Environment.GetEnvironmentVariable("Smtp__Password") 
                        ?? "";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                var errorMsg = "[EMAIL CONFIG ERROR] No se encontró la API Key de Brevo (BREVO_API_KEY).";
                Console.WriteLine(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            Console.WriteLine($"[EMAIL SERVICE] Enviando comprobante a '{emailDestino}' vía API REST de Brevo...");

            var payload = new
            {
                sender = new { name = "Mi POS", email = "no-reply@mipos.com" }, // Cambiar por tu mail verificado en Brevo si aplica
                to = new[] { new { email = emailDestino } },
                subject = $"Comprobante de Pago #{comprobanteId}",
                htmlContent = $@"
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
                    </html>"
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", apiKey);
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[EMAIL SERVICE OK] Comprobante enviado exitosamente a '{emailDestino}' vía API.");
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[EMAIL SERVICE ERROR] Brevo API respondió {response.StatusCode}: {errorBody}");
                throw new HttpRequestException($"Error de Brevo API: {errorBody}");
            }
        }
    }
}