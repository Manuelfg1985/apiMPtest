using MiPos.App.Services;
using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace MiPos.App
{
    public partial class MainPage : ContentPage
    {
        private string _montoTexto = string.Empty;
        private readonly ApiService _apiService;
        private const int MAX_MONTO_LENGTH = 8;
        private const string MONTO_FORMAT = "$ {0:N0}";
        private const string DEFAULT_DISPLAY = "$ 0,00";

        public MainPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
            // TxtEmail.TextChanged += OnEmailTextChanged; // Comentar si no tienes el método
        }

        private void OnNumeroClicked(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                if (_montoTexto.Length >= MAX_MONTO_LENGTH) return;

                if (_montoTexto == "0" && btn.Text != ".")
                {
                    _montoTexto = btn.Text;
                }
                else
                {
                    _montoTexto += btn.Text;
                }

                ActualizarDisplay();
            }
        }

        private void OnBorrarClicked(object sender, EventArgs e)
        {
            _montoTexto = string.Empty;
            ActualizarDisplay();
            // BtnCobrar.Focus();
        }

        private void ActualizarDisplay()
        {
            if (decimal.TryParse(_montoTexto, out decimal monto))
            {
                LblMonto.Text = string.Format(MONTO_FORMAT, monto);
            }
            else
            {
                LblMonto.Text = DEFAULT_DISPLAY;
            }
        }

        private async void OnCobrarClicked(object sender, EventArgs e)
        {
            // Remove email validation if TxtEmail doesn't exist or you don't need it
            /*
            if (!IsValidEmail(TxtEmail.Text))
            {
                await DisplayAlert("Error", "Ingrese un email válido.", "OK");
                TxtEmail.Focus();
                return;
            }
            */

            if (!decimal.TryParse(_montoTexto, out decimal monto) || monto <= 0)
            {
                await DisplayAlert("Error", "Ingrese un monto válido mayor a cero.", "OK");
                return;
            }

            try
            {
                BtnCobrar.IsEnabled = false;

                // Remove the email parameter if not needed
                var respuesta = await _apiService.CrearOrdenQRAsync(monto, TxtEmail?.Text ?? string.Empty);

                if (respuesta != null)
                {
                    await Navigation.PushAsync(new QrPage(respuesta.QrData, respuesta.IntentId, monto));
                }
                else
                {
                    await DisplayAlert("Error", "No se pudo conectar con el servidor de cobro.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Ocurrió un error inesperado. Intente nuevamente.", "OK");
            }
            finally
            {
                BtnCobrar.IsEnabled = true;
            }
        }

        // Remove this method if you don't have a TxtEmail control
        /*
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        */
    }
}