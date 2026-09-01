using System.Net.Http.Json;
using MiPos.Shared.DTOs;

namespace MiPos.App.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        // NOTA: Para emulador de Android usa "http://10.0.2.2:5015". Para Windows usa "http://localhost:5015".
        private const string BaseUrl = "https://mipos-api-kpai.onrender.com"; 

        public ApiService()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        }

        public async Task<CrearCobroResponseDto?> CrearOrdenQRAsync(decimal monto, string emailCliente)
        {
            try
            {
                var request = new CrearCobroRequestDto
                {
                    Monto = monto,
                    EmailCliente = emailCliente
                };

                var response = await _httpClient.PostAsJsonAsync("/api/Cobros/crear-qr", request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CrearCobroResponseDto>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error conectando a la API: {ex.Message}");
            }

            return null;
        }
    }
}