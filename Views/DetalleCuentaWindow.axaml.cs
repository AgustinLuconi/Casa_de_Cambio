using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.ViewModels;
using SistemaCambio.Views.Helpers;

namespace SistemaCambio.Views
{
    public partial class DetalleCuentaWindow : Window
    {
        public DetalleCuentaWindow()
        {
            InitializeComponent();
        }

        public DetalleCuentaWindow(int cuentaId)
        {
            InitializeComponent();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var dialogService = new WindowDialogService(this);
            var viewModel = new DetalleCuentaViewModel(apiClient, cuentaId, dialogService);
            DataContext = viewModel;

            viewModel.SolicitarCierre += Close;
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
