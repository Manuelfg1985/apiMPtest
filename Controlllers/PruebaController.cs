using MercadoPago.Client.Payment;
using Microsoft.AspNetCore.Mvc;

namespace MiPos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PruebaController : ControllerBase
    {
        [HttpGet("verificar-mercadopago")]
        public IActionResult VerificarConfiguracion()
        {
            var client = new PaymentClient();
            return Ok(new { Mensaje = "SDK de Mercado Pago cargado e instanciado correctamente." });
        }
    }
}