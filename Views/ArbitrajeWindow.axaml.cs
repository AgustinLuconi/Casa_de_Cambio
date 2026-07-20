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

            CuentaAutoComplete.Configurar(cmbCuentaCompra);
            CuentaAutoComplete.Configurar(cmbCuentaVenta);

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var offlineService = App.Services.GetRequiredService<IOfflineOperacionService>();
            var viewModel = new ArbitrajeViewModel(apiClient, offlineService);
            DataContext = viewModel;

            viewModel.OperacionGuardada += (idCompra, idVenta, isOffline, mensaje) =>
            {
                if (isOffline)
                    NotificationService.Warning("Guardada offline", mensaje);
                else
                    NotificationService.Success("Arbitraje registrado", $"Compra OP-{idCompra:D5} / Venta OP-{idVenta:D5}");
            };
            viewModel.SolicitarCierre += Close;

            // Sincroniza la selección de cuenta del ViewModel con los AutoCompleteBox
            // (no se puede bindear CuentaMonedaTag por TwoWay a Configurar/Seleccionar sin pasar por el helper).
            DataContextChanged += (_, _) =>
            {
                if (DataContext is not ArbitrajeViewModel vm) return;
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ArbitrajeViewModel.CuentasCompra))
                        cmbCuentaCompra.ItemsSource = vm.CuentasCompra;
                    if (e.PropertyName == nameof(ArbitrajeViewModel.CuentaAcreditaCompra))
                        CuentaAutoComplete.Seleccionar(cmbCuentaCompra, vm.CuentaAcreditaCompra);
                    if (e.PropertyName == nameof(ArbitrajeViewModel.CuentasVenta))
                        cmbCuentaVenta.ItemsSource = vm.CuentasVenta;
                    if (e.PropertyName == nameof(ArbitrajeViewModel.CuentaDebitaVenta))
                        CuentaAutoComplete.Seleccionar(cmbCuentaVenta, vm.CuentaDebitaVenta);
                };
            };

            cmbCuentaCompra.LostFocus += (_, _) =>
            {
                if (DataContext is ArbitrajeViewModel vm)
                    vm.CuentaAcreditaCompra = CuentaAutoComplete.ObtenerSeleccion(cmbCuentaCompra);
            };
            cmbCuentaVenta.LostFocus += (_, _) =>
            {
                if (DataContext is ArbitrajeViewModel vm)
                    vm.CuentaDebitaVenta = CuentaAutoComplete.ObtenerSeleccion(cmbCuentaVenta);
            };
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
