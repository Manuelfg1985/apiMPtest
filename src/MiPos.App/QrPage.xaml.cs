using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using QRCoder;

namespace MiPos.App
{
    public class PagoAprobadoEventDto
    {
        [JsonPropertyName("intentId")]
        public string IntentId { get; set; } = string.Empty;

        [JsonPropertyName("paymentId")]
        public long PaymentId { get; set; }

        [JsonPropertyName("monto")]
        public decimal Monto { get; set; }

        [JsonPropertyName("fecha")]
        public string Fecha { get; set; } = string.Empty;
    }

    public class EstadoPagoResponseDto
    {
        [JsonPropertyName("aprobado")]
        public bool Aprobado { get; set; }

        [JsonPropertyName("intentId")]
        public string IntentId { get; set; } = string.Empty;

        [JsonPropertyName("paymentId")]
        public long PaymentId { get; set; }

        [JsonPropertyName("monto")]
        public decimal Monto { get; set; }
    }

    public partial class QrPage : ContentPage
    {
        private HubConnection? _hubConnection;
        private readonly HttpClient _httpClient;
        private readonly string _intentId;
        private readonly decimal _monto;
        private readonly string _emailCliente;
        private CancellationTokenSource? _pollingCts;
        private bool _pagoProcesado = false;

        private const string ApiBaseUrl = "https://mipos-api-kpai.onrender.com";

        public QrPage(string qrData, string intentId, decimal monto, string emailCliente = "")
        {
            InitializeComponent();
            _intentId = intentId;
            _monto = monto;
            _emailCliente = string.IsNullOrWhiteSpace(emailCliente) ? "manuelfg2@gmail.com" : emailCliente;
            _httpClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };

            LblMonto.Text = $"$ {monto:N2}";

            GenerarQrImage(qrData);
            IniciarSignalR();
            IniciarPollingRespaldo();
        }

        private void GenerarQrImage(string contenido)
        {
            try
            {
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);
                    PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                    byte[] qrCodeAsPngByteARR = qrCode.GetGraphic(20);

                    ImgQr.Source = ImageSource.FromStream(() => new MemoryStream(qrCodeAsPngByteARR));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR QR GENERATION] {ex.Message}");
            }
        }

        private async void IniciarSignalR()
        {
            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{ApiBaseUrl}/pagohub")
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On<PagoAprobadoEventDto>("PagoAprobado", (datosPago) =>
                {
                    if (datosPago != null && (datosPago.IntentId == _intentId || string.IsNullOrEmpty(_intentId)))
                    {
                        ProcesarPagoExitoso(datosPago.Monto > 0 ? datosPago.Monto : _monto, datosPago.PaymentId.ToString());
                    }
                });

                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SIGNALR ERROR] {ex.Message}");
            }
        }

        private void IniciarPollingRespaldo()
        {
            _pollingCts = new CancellationTokenSource();
            var token = _pollingCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !_pagoProcesado)
                {
                    try
                    {
                        await Task.Delay(3000, token);

                        var response = await _httpClient.GetFromJsonAsync<EstadoPagoResponseDto>(
                            $"/api/Cobros/estado-pago/{_intentId}", 
                            token);

                        if (response != null && response.Aprobado)
                        {
                            ProcesarPagoExitoso(response.Monto > 0 ? response.Monto : _monto, response.PaymentId.ToString());
                            break;
                        }
                    }
                    catch (TaskCanceledException) { break; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[POLLING ERROR] {ex.Message}");
                    }
                }
            }, token);
        }

        private void ProcesarPagoExitoso(decimal monto, string comprobanteId)
        {
            if (_pagoProcesado) return;
            _pagoProcesado = true;

            _pollingCts?.Cancel();

            _ = Task.Run(async () =>
            {
                await DespacharEmailAsync(monto, comprobanteId);
            });

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                LblEstado.Text = "¡PAGO CONFIRMADO!";
                LblEstado.TextColor = Colors.Green;

                await this.DisplayAlertAsync("Éxito", $"¡Pago por ${monto:N2} aprobado correctamente!", "Aceptar");
                await Navigation.PopAsync();
            });
        }

        private async Task DespacharEmailAsync(decimal monto, string comprobanteId)
        {
            try
            {
                var payload = new
                {
                    emailDestino = _emailCliente,
                    comprobanteId = string.IsNullOrEmpty(comprobanteId) ? _intentId : comprobanteId,
                    monto = monto
                };

                await _httpClient.PostAsJsonAsync("/api/Cobros/notificar-pago", payload);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] {ex.Message}");
            }
        }

        private async void OnSimularPagoClicked(object? sender, EventArgs e)
        {
            try
            {
                var payload = new
                {
                    comprobanteId = _intentId,
                    monto = _monto,
                    emailDestino = _emailCliente
                };

                await _httpClient.PostAsJsonAsync("/api/Cobros/webhook-mercadopago", payload);
            }
            catch (Exception ex)
            {
                await this.DisplayAlertAsync("Error", $"No se pudo simular el pago: {ex.Message}", "OK");
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            _pollingCts?.Cancel();
            _pollingCts?.Dispose();

            if (_hubConnection != null)
            {
                try
                {
                    await _hubConnection.StopAsync();
                    await _hubConnection.DisposeAsync();
                }
                catch { }
            }

            _httpClient.Dispose();
        }

        private async void OnVolverClicked(object? sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}