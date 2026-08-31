using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MercadoPago.Client;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Resource.Payment;
using MercadoPago.Resource.Preference;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MiPos.API.Hubs;
using MiPos.API.Services;

namespace MiPos.API.Controllers
{
    public class CrearQrDto
    {
        public decimal Monto { get; set; }
        public string? EmailCliente { get; set; }
    }

    public class NotificacionPagoDto
    {
        public string EmailDestino { get; set; } = string.Empty;
        public string ComprobanteId { get; set; } = string.Empty;
        public decimal Monto { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class CobrosController : ControllerBase
    {
        private readonly IHubContext<PagoHub> _hubContext;
        private readonly IEmailService _emailService;

        public CobrosController(IHubContext<PagoHub> hubContext, IEmailService emailService)
        {
            _hubContext = hubContext;
            _emailService = emailService;
        }

        [HttpPost("crear-qr")]
        public async Task<IActionResult> CrearQr([FromBody] CrearQrDto dto)
        {
            try
            {
                if (dto.Monto < 15)
                {
                    return BadRequest(new { error = "El monto mínimo para cobrar con QR es de $15 ARS." });
                }

                var intentId = Guid.NewGuid().ToString();

                var request = new PreferenceRequest
                {
                    Items = new List<PreferenceItemRequest>
                    {
                        new PreferenceItemRequest
                        {
                            Title = "Cobro POS",
                            Quantity = 1,
                            CurrencyId = "ARS",
                            UnitPrice = dto.Monto
                        }
                    },
                    ExternalReference = intentId,
                    NotificationUrl = "https://mipos-api-kpai.onrender.com/api/Cobros/webhook-mp",
                    Payer = new PreferencePayerRequest
                    {
                        Email = string.IsNullOrWhiteSpace(dto.EmailCliente) ? "cliente@mipos.com" : dto.EmailCliente
                    },
                    Metadata = new Dictionary<string, object>
                    {
                        { "email_cliente", dto.EmailCliente ?? "" }
                    }
                };

                var client = new PreferenceClient();
                Preference preference = await client.CreateAsync(request);

                Console.WriteLine($"[QR CREADO] IntentID: {intentId} | InitPoint: {preference.InitPoint}");

                return Ok(new
                {
                    intentId = intentId,
                    qrData = preference.InitPoint,
                    initPoint = preference.InitPoint
                });
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("MercadoPago"))
            {
                Console.WriteLine($"[MP ERROR] {ex.GetType().Name}: {ex.Message}");

                var statusCodeProp = ex.GetType().GetProperty("StatusCode");
                var apiResponseProp = ex.GetType().GetProperty("ApiResponse");

                int statusCode = 500;
                if (statusCodeProp?.GetValue(ex) is int statusInt)
                {
                    statusCode = statusInt;
                }
                else if (statusCodeProp?.GetValue(ex) is System.Net.HttpStatusCode httpStatus)
                {
                    statusCode = (int)httpStatus;
                }

                string? detalle = null;
                if (apiResponseProp?.GetValue(ex) is object apiResp)
                {
                    var contentProp = apiResp.GetType().GetProperty("Content");
                    detalle = contentProp?.GetValue(apiResp)?.ToString();
                }

                return StatusCode(statusCode, new
                {
                    error = "Error en la plataforma de Mercado Pago",
                    detalle = detalle ?? ex.Message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR GENERAL] {ex.Message}");
                return StatusCode(500, new { error = "No se pudo conectar con el servicio de cobro", detalle = ex.Message });
            }
        }

        [HttpPost("webhook-mp")]
        public async Task<IActionResult> WebhookMp([FromQuery] string? type, [FromQuery(Name = "data.id")] string? dataId)
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                Console.WriteLine($"[WEBHOOK RECIBIDO] Type: {type} | DataID: {dataId} | Body: {body}");

                string idToFetch = !string.IsNullOrEmpty(dataId) ? dataId : extractPaymentIdFromBody(body);

                if ((type == "payment" || !string.IsNullOrEmpty(idToFetch)) && long.TryParse(idToFetch, out long paymentId))
                {
                    var client = new PaymentClient();
                    Payment payment = await client.GetAsync(paymentId);

                    Console.WriteLine($"[MP PAYMENT STATUS] ID: {payment.Id} | Status: {payment.Status} | ExternalReference: {payment.ExternalReference}");

                    if (payment.Status == PaymentStatus.Approved)
                    {
                        string intentId = payment.ExternalReference ?? payment.Id.ToString()!;
                        decimal monto = payment.TransactionAmount ?? 0m;
                        string emailCliente = payment.Payer?.Email ?? "";

                        // 1. Notificar en tiempo real al frontend vía SignalR inmediatamente
                        try
                        {
                            await _hubContext.Clients.All.SendAsync("PagoAprobado", new
                            {
                                intentId = intentId,
                                paymentId = payment.Id,
                                monto = monto,
                                fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                            });

                            Console.WriteLine($"[SIGNALR OK] Evento 'PagoAprobado' emitido para IntentID: {intentId}");
                        }
                        catch (Exception signalrEx)
                        {
                            Console.WriteLine($"[SIGNALR ERROR] No se pudo emitir evento SignalR: {signalrEx.Message}");
                        }

                        // 2. Disparar el envío de correo en segundo plano sin bloquear el flujo principal
                        if (!string.IsNullOrWhiteSpace(emailCliente) && emailCliente != "cliente@mipos.com")
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    Console.WriteLine($"[EMAIL INICIADO] Enviando comprobante a {emailCliente}...");
                                    await _emailService.EnviarComprobanteAsync(
                                        emailCliente,
                                        payment.Id.ToString()!,
                                        monto,
                                        DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                                    );
                                    Console.WriteLine($"[EMAIL OK] Comprobante enviado a {emailCliente}");
                                }
                                catch (Exception emailEx)
                                {
                                    Console.WriteLine($"[EMAIL BACKGROUND ERROR] {emailEx.Message}");
                                }
                            });
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEBHOOK ERROR GENERAL] {ex.Message}");
                // Retornar 200 OK a Mercado Pago para evitar reintentos en bucle
                return Ok();
            }
        }

        [HttpGet("estado-pago/{intentId}")]
        public async Task<IActionResult> ObtenerEstadoPago(string intentId)
        {
            try
            {
                var client = new PaymentClient();
                
                var searchRequest = new SearchRequest
                {
                    Filters = new Dictionary<string, object>
                    {
                        { "external_reference", intentId }
                    }
                };

                var searchResult = await client.SearchAsync(searchRequest);
                var payment = searchResult.Results.FirstOrDefault(p => p.Status == PaymentStatus.Approved);

                if (payment != null)
                {
                    return Ok(new
                    {
                        aprobado = true,
                        intentId = intentId,
                        paymentId = payment.Id,
                        monto = payment.TransactionAmount
                    });
                }

                return Ok(new { aprobado = false, intentId = intentId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONSULTA ESTADO ERROR] {ex.Message}");
                return Ok(new { aprobado = false, error = ex.Message });
            }
        }

        [HttpPost("notificar-pago")]
        public IActionResult NotificarPago([FromBody] NotificacionPagoDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.EmailDestino))
                {
                    return BadRequest(new { error = "El email de destino es requerido." });
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.EnviarComprobanteAsync(
                            dto.EmailDestino,
                            dto.ComprobanteId,
                            dto.Monto,
                            DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EMAIL BACKGROUND ERROR] {ex.Message}");
                    }
                });

                return Ok(new { mensaje = "Notificación registrada. El comprobante se está enviando por correo." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NOTIFICAR PAGO ERROR] {ex.Message}");
                return StatusCode(500, new { error = "Error en procesamiento", detalle = ex.Message });
            }
        }

        private string extractPaymentIdFromBody(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("id", out var idEl))
                {
                    return idEl.ToString();
                }
            }
            catch { }
            return string.Empty;
        }
    }
}