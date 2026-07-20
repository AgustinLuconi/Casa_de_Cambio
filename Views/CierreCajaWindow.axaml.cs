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
    public partial class CierreCajaWindow : Window
    {
        public CierreCajaWindow()
        {
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var offlineService = App.Services.GetRequiredService<IOfflineOperacionService>();
            var dialogService = new WindowDialogService(this);
            var viewModel = new CierreCajaViewModel(apiClient, offlineService, dialogService);
            DataContext = viewModel;
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
