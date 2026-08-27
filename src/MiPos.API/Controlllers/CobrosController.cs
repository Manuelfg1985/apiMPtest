using MercadoPago.Client.Preference;
using MercadoPago.Config;
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
                    ExternalReference = intentId
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