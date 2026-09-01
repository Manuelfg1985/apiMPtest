using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Controls;

namespace MiPos.App
{
    public class CrearQrResponseDto
    {
        [JsonPropertyName("intentId")]
        public string IntentId { get; set; } = string.Empty;

        [JsonPropertyName("qrData")]
        public string QrData { get; set; } = string.Empty;

        [JsonPropertyName("monto")]
        public decimal Monto { get; set; }
    }

    public partial class MainPage : ContentPage
    {
        private string _montoTexto = "";
        private readonly HttpClient _httpClient;
        private const string ApiBaseUrl = "https://mipos-api-kpai.onrender.com";

        public MainPage()
        {
            InitializeComponent();
            _httpClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
        }

        private void OnNumeroClicked(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                if (_montoTexto.Length >= 9) return; // Límite de dígitos
                _montoTexto += button.Text;
                ActualizarLabelMonto();
            }
        }

        private void OnBorrarClicked(object? sender, EventArgs e)
        {
            _montoTexto = "";
            ActualizarLabelMonto();
        }

        private void ActualizarLabelMonto()
        {
            if (decimal.TryParse(_montoTexto, out decimal montoCentavos))
            {
                decimal montoReal = montoCentavos / 100m;
                LblMonto.Text = string.Format(CultureInfo.GetCultureInfo("es-AR"), "$ {0:N2}", montoReal);
            }
            else
            {
                LblMonto.Text = "$ 0,00";
            }
        }

        private async void OnCobrarClicked(object? sender, EventArgs e)
        {
            if (!decimal.TryParse(_montoTexto, out decimal montoCentavos) || montoCentavos <= 0)
            {
                await this.DisplayAlertAsync("Atención", "Ingrese un monto mayor a cero.", "Aceptar");
                return;
            }

            decimal montoReal = montoCentavos / 100m;
            string emailCliente = TxtEmail.Text?.Trim() ?? "";

            try
            {
                var requestPayload = new
                {
                    monto = montoReal,
                    emailCliente = emailCliente
                };

                // Petición al endpoint con alias para compatibilidad
                var response = await _httpClient.PostAsJsonAsync("/api/Cobros/crear-qr", requestPayload);

                if (response.IsSuccessStatusCode)
                {
                    var resultado = await response.Content.ReadFromJsonAsync<CrearQrResponseDto>();
                    if (resultado != null)
                    {
                        // Navegar a la pantalla del QR pasando los parámetros
                        await Navigation.PushAsync(new QrPage(resultado.QrData, resultado.IntentId, resultado.Monto, emailCliente));
                    }
                }
                else
                {
                    await this.DisplayAlertAsync("Error", "No se pudo generar el cobro QR en el servidor.", "Aceptar");
                }
            }
            catch
            {
                await this.DisplayAlertAsync("Error de Conexión", "Verifique su conexión a Internet o el estado de la API.", "Aceptar");
            }
        }
    }
}