using Xunit;
using FluentAssertions;
using MiPos.Shared.DTOs;

namespace MiPos.Tests
{
    public class PruebaServicioCobro
    {
        [Fact]
        public void ValidarSolicitudCobro_MontoValido_DebePasarValidacion()
        {
            var solicitud = new CrearCobroRequestDto
            {
                Monto = 1500.50m,
                EmailCliente = "cliente@ejemplo.com"
            };

            bool esValido = solicitud.Monto > 0 && !string.IsNullOrEmpty(solicitud.EmailCliente);

            esValido.Should().BeTrue();
        }
    }
}
