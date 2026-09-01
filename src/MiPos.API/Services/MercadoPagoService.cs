using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MiPos.API.Services;

public class MercadoPagoService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MercadoPagoService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string?> CrearOrdenQrAsync(string externalReference, decimal monto, string descripcion)
    {
        // Obtener AccessToken, UserId y ExternalPosId (compatibilidad con appsettings y Render)
        var accessToken = _configuration["MercadoPago:AccessToken"] 
            ?? _configuration["MercadoPago__AccessToken"] 
            ?? Environment.GetEnvironmentVariable("MercadoPago__AccessToken");

        var userId = _configuration["MercadoPago:UserId"] 
            ?? _configuration["MercadoPago__UserId"] 
            ?? Environment.GetEnvironmentVariable("MercadoPago__UserId");

        var posId = _configuration["MercadoPago:ExternalPosId"] 
            ?? _configuration["MercadoPago__ExternalPosId"] 
            ?? Environment.GetEnvironmentVariable("MercadoPago__ExternalPosId") 
            ?? "POS001";

        var notificationUrl = _configuration["MercadoPago:NotificationUrl"] 
            ?? Environment.GetEnvironmentVariable("MercadoPago__NotificationUrl");

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("Falta configurar AccessToken o UserId de Mercado Pago en la API.");
        }

        var url = $"https://api.mercadopago.com/instore/orders/qr/seller/collectors/{userId}/pos/{posId}/qrs";

        var payload = new
        {
            external_reference = externalReference,
            title = descripcion,
            description = "Cobro registrado desde Mi POS",
            total_amount = monto,
            items = new[]
            {
                new
                {
                    sku_number = "ITEM-001",
                    category = "marketplace",
                    title = descripcion,
                    description = "Cobro de venta",
                    unit_price = monto,
                    quantity = 1,
                    unit_measure = "unit",
                    total_amount = monto
                }
            },
            notification_url = string.IsNullOrEmpty(notificationUrl) ? null : notificationUrl
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error de Mercado Pago ({response.StatusCode}): {errorBody}");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonResponse);

        if (doc.RootElement.TryGetProperty("qr_data", out var qrDataProp))
        {
            return qrDataProp.GetString();
        }

        return null;
    }
}