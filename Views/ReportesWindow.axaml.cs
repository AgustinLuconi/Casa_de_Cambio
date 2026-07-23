using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
using System;
using System.IO;

namespace SistemaCambio.Views
{
    public partial class ReportesWindow : Window
    {
        private readonly ReportesViewModel _viewModel;

        public ReportesWindow()
        {
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();

            var apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            _viewModel = new ReportesViewModel(apiClient);
            DataContext = _viewModel;
        }

        private async void BtnExportarOperaciones_Click(object? sender, RoutedEventArgs e)
        {
            var csv = _viewModel.GenerarCsvOperaciones();
            if (csv == null) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Guardar Reporte de Operaciones", SuggestedFileName = $"operaciones_{DateTime.Now:yyyyMMdd}.csv", FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } } });
            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(csv);
            }
        }

        private async void BtnExportarSaldos_Click(object? sender, RoutedEventArgs e)
        {
            var csv = _viewModel.GenerarCsvSaldos();
            if (csv == null) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Guardar Reporte de Saldos", SuggestedFileName = $"saldos_{DateTime.Now:yyyyMMdd}.csv", FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } } });
            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(csv);
            }
        }

        private async void BtnExportarOperacionesPdf_Click(object? sender, RoutedEventArgs e)
        {
            var pdf = _viewModel.GenerarPdfOperaciones();
            if (pdf == null) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Guardar Reporte de Operaciones", SuggestedFileName = $"operaciones_{DateTime.Now:yyyyMMdd}.pdf", FileTypeChoices = new[] { new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } } } });
            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(pdf);
            }
        }

        private async void BtnExportarSaldosPdf_Click(object? sender, RoutedEventArgs e)
        {
            var pdf = _viewModel.GenerarPdfSaldos();
            if (pdf == null) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Guardar Reporte de Saldos", SuggestedFileName = $"saldos_{DateTime.Now:yyyyMMdd}.pdf", FileTypeChoices = new[] { new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } } } });
            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(pdf);
            }
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
