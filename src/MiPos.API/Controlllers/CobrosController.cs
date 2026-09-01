using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MiPos.API.Hubs;
using MiPos.API.Services;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace MiPos.API.Controllers
{
    // DTO para recibir la petición de cobro inicial
    public class CrearCobroDto
    {
        public decimal Monto { get; set; }
        public string EmailCliente { get; set; } = string.Empty;
    }

    // DTO para notificar el pago por email
    public class NotificarPagoDto
    {
        public string EmailDestino { get; set; } = string.Empty;
        public string ComprobanteId { get; set; } = string.Empty;
        public decimal Monto { get; set; }
    }

    // Modelo interno para almacenar temporalmente el estado en memoria
    public class EstadoTransaccion
    {
        public bool Aprobado { get; set; }
        public string IntentId { get; set; } = string.Empty;
        public long PaymentId { get; set; }
        public decimal Monto { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class CobrosController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly IHubContext<PagoHub> _hubContext;

        // Almacenamiento en memoria para mantener el estado de los pagos durante las pruebas/operación
        private static readonly ConcurrentDictionary<string, EstadoTransaccion> _transacciones = new();

        public CobrosController(IEmailService emailService, IHubContext<PagoHub> hubContext)
        {
            _emailService = emailService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Genera la preferencia de pago y los datos del QR
        /// GET/POST: /api/Cobros/crear-intento
        /// </summary>
        [HttpPost("crear-intento")]
        public IActionResult CrearIntento([FromBody] CrearCobroDto request)
        {
            if (request.Monto <= 0)
            {
                return BadRequest(new { mensaje = "El monto debe ser mayor a cero." });
            }

            var intentId = Guid.NewGuid().ToString("N");
            
            // Cadena ficticia/simulada de QR de Mercado Pago para pruebas
            // En producción aquí se integra el SDK de Mercado Pago / Merchant Orders
            var qrString = $"00020101021243650016com.mercadopago0136{intentId}5204000053030325802AR5906MiPos6009BsAs6304ABCD";

            var estadoInicial = new EstadoTransaccion
            {
                IntentId = intentId,
                Aprobado = false,
                Monto = request.Monto
            };

            _transacciones[intentId] = estadoInicial;

            return Ok(new
            {
                intentId = intentId,
                qrData = qrString,
                monto = request.Monto
            });
        }

        /// <summary>
        /// 
        /// Endpoint para Polling desde el Frontend .NET MAUI
        /// GET: /api/Cobros/estado-pago/{intentId}
        /// </summary>
        [HttpGet("estado-pago/{intentId}")]
        public IActionResult ObtenerEstadoPago(string intentId)
        {
            if (_transacciones.TryGetValue(intentId, out var estado))
            {
                return Ok(new
                {
                    aprobado = estado.Aprobado,
                    intentId = estado.IntentId,
                    paymentId = estado.PaymentId,
                    monto = estado.Monto
                });
            }

            return Ok(new { aprobado = false, intentId = intentId, paymentId = 0, monto = 0m });
        }

        /// <summary>
        /// Webhook o simulador de confirmación de pago
        /// POST: /api/Cobros/webhook-mercadopago O /api/Cobros/confirmar-simulacion
        /// </summary>
        [HttpPost("webhook-mercadopago")]
        public async Task<IActionResult> WebhookMercadoPago([FromBody] NotificarPagoDto payload)
        {
            var intentId = payload.ComprobanteId;
            var paymentId = Random.Shared.Next(100000000, 999999999);

            var estadoAprobado = new EstadoTransaccion
            {
                IntentId = intentId,
                Aprobado = true,
                PaymentId = paymentId,
                Monto = payload.Monto
            };

            _transacciones[intentId] = estadoAprobado;

            // Emitir evento por SignalR en tiempo real a la app móvil
            await _hubContext.Clients.All.SendAsync("PagoAprobado", new
            {
                intentId = intentId,
                paymentId = paymentId,
                monto = payload.Monto,
                fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            });

            return Ok(new { status = "processed" });
        }

        /// <summary>
        /// Endpoint que invoca el frontend .NET MAUI para despachar el correo
        /// POST: /api/Cobros/notificar-pago
        /// </summary>
        [HttpPost("notificar-pago")]
        public async Task<IActionResult> NotificarPago([FromBody] NotificarPagoDto request)
        {
            if (string.IsNullOrWhiteSpace(request.EmailDestino))
            {
                return BadRequest(new { mensaje = "El email de destino es obligatorio." });
            }

            var fechaActual = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            try
            {
                // Notifica al email dinámico que viene en el request
                await _emailService.EnviarComprobanteAsync(
                    request.EmailDestino,
                    request.ComprobanteId,
                    request.Monto,
                    fechaActual
                );

                return Ok(new { mensaje = "Comprobante enviado exitosamente." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONTROLLER ERROR] Falló envío a '{request.EmailDestino}': {ex.Message}");
                return StatusCode(500, new { mensaje = "Error al procesar el email.", detalle = ex.Message });
            }
        }
    }
}