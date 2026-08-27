namespace MiPos.Shared.DTOs
{
    public class CrearCobroRequestDto
    {
        public decimal Monto { get; set; }
        public string EmailCliente { get; set; } = string.Empty;
    }
}