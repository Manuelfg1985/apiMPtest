using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MiPos.API.Hubs;
using MiPos.API.Models;
using MiPos.API.Services;

namespace MiPos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CobrosController : ControllerBase
{
    private readonly MercadoPagoService _mercadoPagoService;
    private readonly IHubContext<PagoHub> _hubContext;
    private readonly IEmailService _emailService;

    // Inyección de dependencias a través del constructor
    public CobrosController(
        MercadoPagoService mercadoPagoService,
        IHubContext<PagoHub> hubContext,
        IEmailService emailService)
    {
        _mercadoPagoService = mercadoPagoService;
        _hubContext = hubContext;
        _emailService = emailService;
    }

    /// <summary>
    /// Crea un cobro y genera el QR oficial registrado en Mercado Pago
    /// </summary>
    [HttpPost("crear")]
    public async Task<IActionResult> CrearCobro([FromBody] CrearCobroRequest request)
    {
        if (request == null || request.Monto <= 0)
        {
            return BadRequest(new { mensaje = "El monto ingresado debe ser mayor a 0." });
        }

        // Generar identificador único de la transacción
        var cobroId = Guid.NewGuid().ToString("N");

        try
        {
            // 1. Solicitar el código QR oficial a la API de Mercado Pago
            var qrData = await _mercadoPagoService.CrearOrdenQrAsync(
                externalReference: cobroId,
                monto: request.Monto,
                descripcion: $"Cobro #{cobroId[..6].ToUpper()}"
            );

            if (string.IsNullOrEmpty(qrData))
            {
                return BadRequest(new { mensaje = "Mercado Pago no devolvió una cadena QR válida." });
            }

            // 2. Guardar el cobro en el repositorio local / BD en memoria
            var nuevoCobro = new CobroModel
            {
                Id = cobroId,
                Monto = request.Monto,
                EmailCliente = request.EmailCliente,
                Estado = "PENDIENTE",
                QrData = qrData,
                FechaCreacion = DateTime.UtcNow
            };

            CobrosRepository.Guardar(nuevoCobro);

            // 3. Responder a la app móvil con el cobroId y el qrData oficial de MP
            return Ok(new
            {
                cobroId = nuevoCobro.Id,
                qrData = nuevoCobro.QrData,
                monto = nuevoCobro.Monto
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error al crear orden en MercadoPago: {ex.Message}");
            return StatusCode(500, new { mensaje = "Error al comunicarse con Mercado Pago", detalle = ex.Message });
        }
    }

    /// <summary>
    /// Endpoint para consultar el estado del cobro (Polling)
    /// </summary>
    [HttpGet("estado/{id}")]
    public IActionResult ObtenerEstado(string id)
    {
        var cobro = CobrosRepository.ObtenerPorId(id);
        if (cobro == null)
        {
            return NotFound(new { mensaje = "Cobro no encontrado." });
        }

        return Ok(new
        {
            id = cobro.Id,
            estado = cobro.Estado,
            monto = cobro.Monto,
            emailCliente = cobro.EmailCliente
        });
    }

    /// <summary>
    /// Endpoint de simulación para pruebas manuales/DEMO
    /// </summary>
    [HttpPost("simular-pago/{id}")]
    public async Task<IActionResult> SimularPago(string id)
    {
        var cobro = CobrosRepository.ObtenerPorId(id);
        if (cobro == null)
        {
            return NotFound(new { mensaje = "Cobro no encontrado." });
        }

        cobro.Estado = "APROBADO";
        cobro.FechaPago = DateTime.UtcNow;

        // Notificar por SignalR a la app móvil en tiempo real
        await _hubContext.Clients.All.SendAsync("PagoActualizado", cobro.Id, cobro.Estado);

        // Enviar email de comprobante si se ingresó un correo
        if (!string.IsNullOrWhiteSpace(cobro.EmailCliente))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.EnviarComprobanteAsync(
                    cobro.EmailCliente ?? "", 
                    cobro.Monto, 
                    cobro.Id, 
                    (cobro.FechaPago ?? DateTime.UtcNow).ToString("dd/MM/yyyy HH:mm"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falló envío de email simulado: {ex.Message}");
                }
            });
        }

        return Ok(new { mensaje = "Pago simulado exitosamente", cobro });
    }
}