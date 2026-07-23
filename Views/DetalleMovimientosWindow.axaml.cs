using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

        private async void BtnExportarMovimientosPdf_Click(object? sender, RoutedEventArgs e)
        {
            var pdf = _viewModel.GenerarPdfMovimientos();
            if (pdf == null) { NotificationService.Warning("Sin datos", "No hay movimientos para exportar."); return; }
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Guardar Movimientos",
                SuggestedFileName = $"movimientos_{DateTime.Now:yyyyMMdd}.pdf",
                FileTypeChoices = new[] { new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } } }
            });
            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(pdf);
            }
        }

        private async void BtnExportarMovimientosCsv_Click(object? sender, RoutedEventArgs e)
        {
            var csv = _viewModel.GenerarCsvMovimientos();
            if (csv == null) { NotificationService.Warning("Sin datos", "No hay movimientos para exportar."); return; }
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Guardar Movimientos",
                SuggestedFileName = $"movimientos_{DateTime.Now:yyyyMMdd}.csv",
                FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
            });
            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(csv);
            }
        }
    }
}
