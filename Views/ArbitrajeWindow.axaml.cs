using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using SistemaCambio.ViewModels;
using SistemaCambio.Views.Helpers;

namespace SistemaCambio.Views
{
    public partial class ArbitrajeWindow : Window
    {
        public ArbitrajeWindow()
        {
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var offlineService = App.Services.GetRequiredService<IOfflineOperacionService>();
            var viewModel = new ArbitrajeViewModel(apiClient, offlineService, new WindowDialogService(this));
            DataContext = viewModel;

            viewModel.OperacionGuardada += (idCompra, idVenta, isOffline, mensaje) =>
            {
                if (isOffline)
                    NotificationService.Warning("Guardada offline", mensaje);
                else
                    NotificationService.Success("Arbitraje registrado", $"Compra OP-{idCompra:D5} / Venta OP-{idVenta:D5}");
            };
            viewModel.SolicitarCierre += Close;
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
