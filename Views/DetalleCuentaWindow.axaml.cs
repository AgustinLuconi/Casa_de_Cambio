using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
using SistemaCambio.Views.Helpers;

namespace SistemaCambio.Views
{
    public partial class DetalleCuentaWindow : Window
    {
        private DetalleCuentaViewModel? _viewModel;

        public DetalleCuentaWindow()
        {
            InitializeComponent();
        }

        public DetalleCuentaWindow(int cuentaId)
        {
            InitializeComponent();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            var dialogService = new WindowDialogService(this);
            _viewModel = new DetalleCuentaViewModel(apiClient, cuentaId, dialogService);
            DataContext = _viewModel;

            _viewModel.SolicitarCierre += Close;
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();

        private async void BtnExportarPdf_Click(object? sender, RoutedEventArgs e)
        {
            if (_viewModel == null || sender is not Button btn) return;
            btn.IsEnabled = false;
            try
            {
                var pdf = await _viewModel.GenerarPdfEstadoCuentaAsync();
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Guardar Estado de Cuenta",
                    SuggestedFileName = $"estado_cuenta_{_viewModel.NombreCuenta}_{DateTime.Now:yyyyMMdd}.pdf",
                    FileTypeChoices = new[] { new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } } }
                });
                if (file != null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    await stream.WriteAsync(pdf);
                }
            }
            catch (Exception ex)
            {
                NotificationService.Error("Error al exportar PDF", ex.Message);
            }
            finally { btn.IsEnabled = true; }
        }

        private async void BtnExportarCsv_Click(object? sender, RoutedEventArgs e)
        {
            if (_viewModel == null || sender is not Button btn) return;
            btn.IsEnabled = false;
            try
            {
                var csv = await _viewModel.GenerarCsvEstadoCuentaAsync();
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Guardar Estado de Cuenta",
                    SuggestedFileName = $"estado_cuenta_{_viewModel.NombreCuenta}_{DateTime.Now:yyyyMMdd}.csv",
                    FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
                });
                if (file != null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    await using var writer = new StreamWriter(stream);
                    await writer.WriteAsync(csv);
                }
            }
            catch (Exception ex)
            {
                NotificationService.Error("Error al exportar CSV", ex.Message);
            }
            finally { btn.IsEnabled = true; }
        }
    }
}
