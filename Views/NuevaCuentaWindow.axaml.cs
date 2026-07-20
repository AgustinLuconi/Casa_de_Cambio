using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class NuevaCuentaWindow : Window
    {
        // Columna "LÍMITE ESPECÍFICO": solo visible para cuentas Cliente.
        // Se busca por header (no por índice) para sobrevivir reordenamientos del XAML.
        private Avalonia.Controls.DataGridColumn ColumnaLimite =>
            dgSaldosIniciales.Columns.First(c => c.Header?.ToString() == "LÍMITE ESPECÍFICO");

        public NuevaCuentaWindow() : this(null) { }

        public NuevaCuentaWindow(int cuentaId) : this((int?)cuentaId) { }

        private NuevaCuentaWindow(int? cuentaId)
        {
            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            var viewModel = new NuevaCuentaViewModel(apiClient, cuentaId);
            DataContext = viewModel;

            if (viewModel.EsEdicion)
            {
                Title = "Editar Cuenta";
                txtTitulo.Text = "Editar Cuenta";
                txtSaldosTitulo.Text = "Saldos Actuales por Divisa";
                iconHeader.Kind = Material.Icons.MaterialIconKind.BankTransfer;
            }

            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NuevaCuentaViewModel.EsCliente))
                    ColumnaLimite.IsVisible = viewModel.EsCliente;
                else if (e.PropertyName == nameof(NuevaCuentaViewModel.EsEfectivo))
                {
                    gridMonedaEfectivo.IsVisible = viewModel.EsEfectivo;
                    dgSaldosIniciales.IsVisible = !viewModel.EsEfectivo;
                }
            };

            viewModel.SolicitarCierre += Close;
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
