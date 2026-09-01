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
                var errorMsg = "[EMAIL CONFIG ERROR] No se encontró la API Key de Brevo en las variables de entorno.";
                Console.WriteLine(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            Console.WriteLine($"[EMAIL SERVICE] Enviando comprobante a '{emailDestino}' vía API REST de Brevo...");

            // ATENCIÓN: El objeto 'sender' utiliza tu casilla registrada en Brevo para pasar la validación
            var payload = new
            {
                sender = new { name = "Mi POS", email = "manuelfg2@gmail.com" },
                to = new[] { new { email = emailDestino } },
                subject = $"Comprobante de Pago #{comprobanteId}",
                htmlContent = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <style>
                            body {{ font-family: Arial, sans-serif; color: #333; line-height: 1.6; background-color: #f4f4f4; padding: 20px; }}
                            .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }}
                            .header {{ background-color: #009ee3; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 25px; }}
                            .details {{ background-color: #f9f9f9; padding: 15px; border-radius: 6px; margin: 20px 0; border-left: 4px solid #009ee3; }}
                            .footer {{ font-size: 12px; color: #777; text-align: center; padding: 15px; background: #f1f1f1; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h2 style='margin:0;'>¡Pago Confirmado!</h2>
                            </div>
                            <div class='content'>
                                <p>Hola,</p>
                                <p>Hemos recibido tu pago correctamente. A continuación encuentras el detalle de la transacción:</p>
                                <div class='details'>
                                    <p style='margin: 5px 0;'><strong>N° de Comprobante:</strong> {comprobanteId}</p>
                                    <p style='margin: 5px 0;'><strong>Monto:</strong> ${monto:N2} ARS</p>
                                    <p style='margin: 5px 0;'><strong>Fecha:</strong> {fecha}</p>
                                    <p style='margin: 5px 0;'><strong>Estado:</strong> Aprobado</p>
                                </div>
                                <p>Gracias por tu compra.</p>
                            </div>
                            <div class='footer'>
                                <p>Este es un comprobante generado automáticamente por Mi POS.</p>
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