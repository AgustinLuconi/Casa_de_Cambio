using Avalonia.Controls;
using Avalonia.Interactivity;
using CasaCambio.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
using SistemaCambio.Views.Helpers;

namespace SistemaCambio.Views
{
    public partial class DetalleMovimientosWindow : Window
    {
        private readonly DetalleMovimientosViewModel _viewModel;

        public DetalleMovimientosWindow()
        {
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var dialogService = new WindowDialogService(this);
            _viewModel = new DetalleMovimientosViewModel(apiClient, dialogService);
            DataContext = _viewModel;
        }

        private async void BtnAnular_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: OperacionDto op })
                await _viewModel.AnularAsync(op);
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
