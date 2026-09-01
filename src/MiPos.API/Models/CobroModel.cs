namespace MiPos.API.Models;

public class CobroModel
{
    public string Id { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? EmailCliente { get; set; }
    public string Estado { get; set; } = "PENDIENTE"; // PENDIENTE, APROBADO, RECHAZADO
    public string QrData { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaPago { get; set; }
}