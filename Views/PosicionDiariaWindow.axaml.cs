using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;

namespace SistemaCambio.Views
{
    public partial class PosicionDiariaWindow : Window
    {
        public PosicionDiariaWindow()
        {
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            DataContext = new PosicionDiariaViewModel(apiClient);
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
