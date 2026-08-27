using MercadoPago.Client.Payment;
using MiPos.API.Hubs;
using MiPos.API.Services;
using MiPos.Shared.DTOs;
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
        public async Task<IActionResult> CrearOrdenQR([FromBody] CrearCobroRequestDto request)
        {
            if (request.Monto <= 0)
                return BadRequest("El monto debe ser mayor a cero.");

            string externalReference = Guid.NewGuid().ToString();

            var response = new CrearCobroResponseDto
            {
                IntentId = externalReference,
                QrData = $"https://mpago.la/pos/{externalReference}",
                Status = "pending"
            };

            return Ok(response);
        }

        [HttpPost("notificar-pago")]
        public async Task<IActionResult> NotificarPago([FromBody] NotificacionPagoDto dto)
        {
            // 1. Notificar a la app móvil a través de SignalR
            await _hubContext.Clients.All.SendAsync("PagoConfirmado", dto.IntentId, dto.Monto);

            // 2. Si se proporcionó email, enviar comprobante
            if (!string.IsNullOrEmpty(dto.EmailCliente))
            {
                string fechaActual = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                await _emailService.EnviarComprobanteAsync(dto.EmailCliente, dto.IntentId.Substring(0, 8), dto.Monto, fechaActual);
            }

            return Ok(new { Mensaje = "Pago procesado y notificado con éxito." });
        }
    }

    public class NotificacionPagoDto
    {
        public string IntentId { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string EmailCliente { get; set; } = string.Empty;
    }
}