using Microsoft.AspNetCore.SignalR.Client;
using QRCoder;

namespace MiPos.App
{
    public partial class QrPage : ContentPage
    {
        private HubConnection? _hubConnection;
        private readonly string _intentId;

        public QrPage(string qrData, string intentId, decimal monto)
        {
            InitializeComponent();
            _intentId = intentId;
            LblMonto.Text = $"$ {monto:N0}";

            GenerarQrImage(qrData);
            IniciarSignalR();
        }

        private void GenerarQrImage(string contenido)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);
                PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                byte[] qrCodeAsPngByteARR = qrCode.GetGraphic(20);

                ImgQr.Source = ImageSource.FromStream(() => new MemoryStream(qrCodeAsPngByteARR));
            }
        }

        private async void IniciarSignalR()
        {
            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl("http://localhost:5015/pagohub") // Ajustar IP si ejecutas en emulador Android
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On<string, decimal>("PagoConfirmado", async (intentId, monto) =>
                {
                    if (intentId == _intentId)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            LblEstado.Text = "¡PAGO CONFIRMADO!";
                            LblEstado.TextColor = Microsoft.Maui.Graphics.Colors.Green;
                            await DisplayAlert("Éxito", $"¡Pago por ${monto:N2} aprobado!", "Aceptar");
                            await Navigation.PopAsync();
                        });
                    }
                });

                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error SignalR: {ex.Message}");
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
            }
        }

        private async void OnVolverClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}