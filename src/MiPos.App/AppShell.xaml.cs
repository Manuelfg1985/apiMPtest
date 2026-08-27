namespace MiPos.App
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Registrar la ruta de la pantalla del QR para la navegación
            Routing.RegisterRoute(nameof(QrPage), typeof(QrPage));
        }
    }
}