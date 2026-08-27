using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Payment;
using MercadoPago.Resource.Preference;
using MiPos.API.Hubs;
using MiPos.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace MiPos.API.Controllers
{
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
                var tokenCargado = MercadoPagoConfig.AccessToken;
                var tokenInicio = string.IsNullOrEmpty(tokenCargado) ? "VACIO" : tokenCargado.Substring(0, Math.Min(10, tokenCargado.Length));
                Console.WriteLine($"[DEBUG] Intentando crear QR con Token iniciado en: {tokenInicio}...");

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
                    // IMPORTANTE: Mercado Pago usará esta URL para avisar a tu backend en tiempo real
                    NotificationUrl = "https://mipos-api-kpai.onrender.com/api/Cobros/webhook-mp",
                    // Puedes adjuntar el email en Payer para asociarlo
                    Payer = new PreferencePayerRequest
                    {
                        Email = string.IsNullOrEmpty(dto.EmailCliente) ? "cliente@mipos.com" : dto.EmailCliente
                    }
                };

                var client = new PreferenceClient();
                Preference preference = await client.CreateAsync(request);

                return Ok(new
                {
                    intentId = intentId,
                    qrData = preference.InitPoint,
                    initPoint = preference.InitPoint
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al generar QR en MercadoPago: {ex.Message}");
                return StatusCode(500, new { error = "No se pudo generar el QR", detalle = ex.Message });
            }
        }

        // WEBHOOK REAL QUE INVOCARÁ MERCADO PAGO
        [HttpPost("webhook-mp")]
        public async Task<IActionResult> WebhookMercadoPago([FromQuery] string? type, [FromQuery] string? topic, [FromQuery(Name = "data.id")] string? dataId, [FromQuery] string? id)
        {
            try
            {
                // Mercado Pago envía el ID del pago en data.id o en id según la versión
                string paymentIdStr = dataId ?? id ?? "";
                string eventoTipo = type ?? topic ?? "";

                Console.WriteLine($"[WEBHOOK RECIBIDO] Tipo: {eventoTipo} | Payment ID: {paymentIdStr}");

                // Verificamos si el evento es sobre un pago
                if ((eventoTipo == "payment" || eventoTipo == "collection") && long.TryParse(paymentIdStr, out long paymentId))
                {
                    // Consultamos el estado real del pago mediante la SDK
                    var paymentClient = new PaymentClient();
                    Payment payment = await paymentClient.GetAsync(paymentId);

                    Console.WriteLine($"[ESTADO PAGO MP] ID: {payment.Id} | Estado: {payment.Status} | ExternalRef: {payment.ExternalReference}");

                    // Si el pago fue APROBADO
                    if (payment.Status == PaymentStatus.Approved)
                    {
                        string intentId = payment.ExternalReference ?? Guid.NewGuid().ToString();
                        decimal monto = payment.TransactionAmount ?? 0m;
                        string emailCliente = payment.Payer?.Email ?? "";

                        // 1. Notificamos a la App móvil / Frontend vía SignalR
                        await _hubContext.Clients.All.SendAsync("PagoConfirmado", intentId, monto);

                        // 2. Enviamos el comprobante por Email si hay destinatario
                        if (!string.IsNullOrEmpty(emailCliente) && !emailCliente.Contains("@mipos.com"))
                        {
                            string fechaActual = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                            string comprobanteId = payment.Id.HasValue ? payment.Id.Value.ToString() : intentId.Substring(0, 8);

                            await _emailService.EnviarComprobanteAsync(emailCliente, comprobanteId, monto, fechaActual);
                            Console.WriteLine($"[EMAIL ENVIADO] Comprobante enviado a {emailCliente}");
                        }
                    }
                }

                // Responder siempre HTTP 200 OK a Mercado Pago
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR WEBHOOK] {ex.Message}");
                // Retornar 200 OK de todos modos para que MP no reintente agresivamente si fue error interno
                return Ok();
            }
        }

        // Endpoint manual (mantenido por compatibilidad si lo llamas manualmente desde la app)
        [HttpPost("notificar-pago")]
        public async Task<IActionResult> NotificarPago([FromBody] NotificacionPagoDto dto)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("PagoConfirmado", dto.IntentId, dto.Monto);

                if (!string.IsNullOrEmpty(dto.EmailCliente))
                {
                    string fechaActual = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                    string comprobanteId = dto.IntentId.Length >= 8 ? dto.IntentId.Substring(0, 8) : dto.IntentId;

                    await _emailService.EnviarComprobanteAsync(dto.EmailCliente, comprobanteId, dto.Monto, fechaActual);
                }

                return Ok(new { mensaje = "Pago procesado y notificado con éxito." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar notificación/email: {ex.Message}");
                return StatusCode(500, new { error = "Error en envío de correo o proceso", detalle = ex.Message });
            }
        }
    }

    public class CrearQrDto
    {
        public decimal Monto { get; set; }
        public string EmailCliente { get; set; } = string.Empty;
    }

    public class NotificacionPagoDto
    {
        public string IntentId { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string EmailCliente { get; set; } = string.Empty;
    }
}