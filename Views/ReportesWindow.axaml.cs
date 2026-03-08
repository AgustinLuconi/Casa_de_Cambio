using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using CasaCambio.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SistemaCambio.Views
{
    public partial class ReportesWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;

        public ReportesWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            dpDesdeOp.SelectedDate = new DateTimeOffset(DateTime.Today.AddDays(-30));
            dpHastaOp.SelectedDate = new DateTimeOffset(DateTime.Today);
        }

        private async void BtnGenerarOperaciones_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var fechaDesde = dpDesdeOp.SelectedDate?.Date ?? DateTime.Today.AddDays(-30);
                var fechaHasta = (dpHastaOp.SelectedDate?.Date ?? DateTime.Today).AddDays(1);
                var response = await _apiClient.ObtenerOperacionesAsync(fechaDesde, fechaHasta, pageSize: 500);
                dgOperaciones.ItemsSource = response.Items;
            }
            catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
        }

        private async void BtnGenerarSaldos_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var itemTipo = cmbTipoSaldos.SelectedItem as ComboBoxItem;
                string tipo = itemTipo?.Content?.ToString() ?? "Todos";
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                if (tipo != "Todos") cuentas = cuentas.Where(c => c.Tipo == tipo).ToList();
                dgSaldos.ItemsSource = cuentas.OrderBy(c => c.Nombre).ToList();
            }
            catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
        }

        private async void BtnExportarOperaciones_Click(object? sender, RoutedEventArgs e)
        {
            var operaciones = dgOperaciones.ItemsSource as IEnumerable<OperacionDto>;
            if (operaciones == null || !operaciones.Any()) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Guardar Reporte de Operaciones", SuggestedFileName = $"operaciones_{DateTime.Now:yyyyMMdd}.csv", FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } } });
            if (file != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("ID,Fecha,Tipo,MontoOrigen,MontoDestino,Cotizacion,Observaciones");
                foreach (var op in operaciones) sb.AppendLine($"{op.Id},{op.Fecha:yyyy-MM-dd HH:mm},{op.TipoOperacion},{op.MontoTotalOrigen},{op.MontoTotalDestino},{op.CotizacionAplicada},\"{op.Observaciones}\"");
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(sb.ToString());
            }
        }

        private async void BtnExportarSaldos_Click(object? sender, RoutedEventArgs e)
        {
            var cuentas = dgSaldos.ItemsSource as IEnumerable<CuentaDto>;
            if (cuentas == null || !cuentas.Any()) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Guardar Reporte de Saldos", SuggestedFileName = $"saldos_{DateTime.Now:yyyyMMdd}.csv", FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } } });
            if (file != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("ID,Cuenta,Tipo,Moneda,Saldo");
                foreach (var c in cuentas)
                {
                    if (c.Saldos.Any()) { foreach (var s in c.Saldos) sb.AppendLine($"{c.Id},\"{c.Nombre}\",{c.Tipo},{s.Moneda},{s.Saldo}"); }
                    else sb.AppendLine($"{c.Id},\"{c.Nombre}\",{c.Tipo},,0");
                }
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(sb.ToString());
            }
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
