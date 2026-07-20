using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;

namespace SistemaCambio.Views
{
    public partial class ArqueoWindow : Window
    {
        public ArqueoWindow()
        {
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var viewModel = new ArqueoViewModel(apiClient);
            DataContext = viewModel;

            viewModel.SolicitarCierre += Close;
        }

        private void BtnSalir_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
