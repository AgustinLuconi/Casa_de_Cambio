using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Views.Helpers;
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
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();
            dpDesdeOp.SelectedDate = new DateTimeOffset(DateTime.Today.AddDays(-30));
            dpHastaOp.SelectedDate = new DateTimeOffset(DateTime.Today);
        }

        private async void BtnGenerarOperaciones_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // El servidor compara contra una columna timestamptz: las fechas DEBEN
                // viajar como UTC, o Npgsql rechaza el Kind=Unspecified y no llega nada.
                var fechaDesde = DateTime.SpecifyKind(
                    dpDesdeOp.SelectedDate?.Date ?? DateTime.Today.AddDays(-30), DateTimeKind.Utc);
                var fechaHasta = DateTime.SpecifyKind(
                    (dpHastaOp.SelectedDate?.Date ?? DateTime.Today).AddDays(1), DateTimeKind.Utc);
                var response = await _apiClient.ObtenerOperacionesAsync(fechaDesde, fechaHasta, pageSize: 500);
                dgOperaciones.ItemsSource = response.Items;
            }
            catch (Exception ex) { AppLogger.Warn("BtnGenerarOperaciones_Click", ex); }
        }

        private async void BtnGenerarSaldos_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var itemTipo = cmbTipoSaldos.SelectedItem as ComboBoxItem;
                string tipo = itemTipo?.Content?.ToString() ?? "Todos";
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                cuentas = cuentas.Where(c => c.Tipo != "Externo").ToList();
                if (tipo != "Todos") cuentas = cuentas.Where(c => c.Tipo == tipo).ToList();

                // Aplanar: una fila por (cuenta, saldo). CuentaDto no expone Moneda/Saldo
                // directamente — viven en la lista Saldos (multi-moneda por cuenta).
                var filas = new List<SaldoReporteRow>();
                foreach (var c in cuentas.OrderBy(c => c.Nombre))
                {
                    if (c.Saldos.Count > 0)
                    {
                        foreach (var s in c.Saldos.OrderBy(s => s.Moneda))
                            filas.Add(new SaldoReporteRow
                            {
                                Id = c.Id, Nombre = c.Nombre, Tipo = c.Tipo,
                                Moneda = s.Moneda, Saldo = s.Saldo
                            });
                    }
                    else
                    {
                        // Cuenta sin saldos: una fila placeholder para que siga visible
                        filas.Add(new SaldoReporteRow
                        {
                            Id = c.Id, Nombre = c.Nombre, Tipo = c.Tipo,
                            Moneda = "—", Saldo = 0
                        });
                    }
                }
                dgSaldos.ItemsSource = filas;
            }
            catch (Exception ex) { AppLogger.Warn("BtnGenerarSaldos_Click", ex); }
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
            var filas = dgSaldos.ItemsSource as IEnumerable<SaldoReporteRow>;
            if (filas == null || !filas.Any()) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Guardar Reporte de Saldos", SuggestedFileName = $"saldos_{DateTime.Now:yyyyMMdd}.csv", FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } } });
            if (file != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("ID,Cuenta,Tipo,Moneda,Saldo");
                foreach (var f in filas)
                    sb.AppendLine($"{f.Id},\"{f.Nombre}\",{f.Tipo},{f.Moneda},{f.Saldo}");
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(sb.ToString());
            }
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();
    }

    // Fila aplanada para la grilla de "Saldos por Cuenta": expone Moneda y Saldo
    // como propiedades de primer nivel que el DataGrid puede bindear directamente.
    public class SaldoReporteRow
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Moneda { get; set; } = "";
        public decimal Saldo { get; set; }
    }
}
