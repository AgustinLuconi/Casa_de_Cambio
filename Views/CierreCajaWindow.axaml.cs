using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using CasaCambio.Shared.DTOs;
using System;
using System.Linq;

namespace SistemaCambio.Views
{
    public class SaldoCajaItem
    {
        public string Nombre { get; set; } = "";
        public string SaldoFormatted { get; set; } = "";
        public Avalonia.Media.IBrush ColorBrush { get; set; } = Avalonia.Media.Brushes.Gray;
    }

    public partial class CierreCajaWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private int? _cierreId;

        public CierreCajaWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            txtFecha.Text = DateTime.Today.ToString("dddd, dd 'de' MMMM 'de' yyyy");
            CargarSaldosDinamicosAsync();
            CargarCierreExistenteAsync();
            CargarOperacionesDiaAsync();
        }

        private async void CargarCierreExistenteAsync()
        {
            try
            {
                var cierre = await _apiClient.ObtenerCierreHoyAsync();
                if (cierre != null) MostrarCierre(cierre);
            }
            catch { }
        }

        private async void BtnGenerar_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var cierre = await _apiClient.GenerarCierreAsync(txtObservaciones.Text ?? "");
                MostrarCierre(cierre);
                borderEstado.IsVisible = true;
                btnCerrarDefinitivo.IsEnabled = true;
                NotificationService.Info("Cierre generado", "Revise los datos y cierre definitivamente");
            }
            catch (Exception ex) { NotificationService.Error("Error al generar cierre", ex.Message); }
        }

        private void MostrarCierre(CierreCajaDto cierre)
        {
            _cierreId = cierre.Id;
            txtCantidadCompras.Text = cierre.CantidadCompras.ToString();
            txtComprasUSD.Text = $"${cierre.TotalComprasUSD:N2}";
            txtComprasARS.Text = $"${cierre.TotalComprasARS:N2}";
            txtCantidadVentas.Text = cierre.CantidadVentas.ToString();
            txtVentasUSD.Text = $"${cierre.TotalVentasUSD:N2}";
            txtVentasARS.Text = $"${cierre.TotalVentasARS:N2}";
            txtTotalDiferencias.Text = $"${cierre.TotalDiferencias:N2}";
            txtTotalDiferencias.Foreground = cierre.TotalDiferencias == 0
                ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#16A34A"))
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DC2626"));

            CargarSaldosDinamicosAsync();
            CargarOperacionesDiaAsync();
            borderEstado.IsVisible = true;

            if (cierre.Cerrado)
            {
                borderEstado.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#16A34A20"));
                borderEstado.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#16A34A"));
                txtEstado.Text = "Cierre cerrado definitivamente";
                txtEstado.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#16A34A"));
                btnCerrarDefinitivo.IsEnabled = false;
                btnGenerar.IsEnabled = false;
                txtObservaciones.IsReadOnly = true;
            }
            else btnCerrarDefinitivo.IsEnabled = true;
        }

        private async void CargarSaldosDinamicosAsync()
        {
            try
            {
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                var cajas = cuentas.Where(c => c.Tipo == "Efectivo").ToList();
                var saldosGrouped = cajas.SelectMany(c => c.Saldos)
                    .GroupBy(s => s.Moneda)
                    .Select(g => new { Moneda = g.Key, Saldo = g.Sum(x => x.Saldo) })
                    .ToList();

                var items = new System.Collections.Generic.List<SaldoCajaItem>();
                string[] colors = { "#3B82F6", "#16A34A", "#D97706", "#DC2626", "#8b5cf6", "#14b8a6" };
                int brushIndex = 0;
                var codigosEspeciales = new[] { "ARS", "USD", "EUR" };
                var todasMonedas = saldosGrouped.Select(s => s.Moneda).Union(codigosEspeciales).Distinct()
                    .OrderBy(x => { var idx = Array.IndexOf(codigosEspeciales, x); return idx == -1 ? 99 : idx; }).ThenBy(x => x);

                foreach (var moneda in todasMonedas)
                {
                    var saldoObj = saldosGrouped.FirstOrDefault(s => s.Moneda == moneda);
                    decimal saldoMonto = saldoObj?.Saldo ?? 0m;
                    var brush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(colors[brushIndex % colors.Length]));
                    string nombreLargo = moneda switch { "ARS" => "PESOS (ARS)", "USD" => "DOLARES (USD)", "EUR" => "EUROS (EUR)", _ => moneda };
                    items.Add(new SaldoCajaItem { Nombre = nombreLargo, SaldoFormatted = $"${saldoMonto:N2}", ColorBrush = brush });
                    brushIndex++;
                }
                icSaldos.ItemsSource = items;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        private async void BtnCerrarDefinitivo_Click(object? sender, RoutedEventArgs e)
        {
            if (_cierreId == null) return;

            var offlineService = App.Services.GetRequiredService<OfflineOperacionService>();
            int pendientes = await offlineService.ObtenerPendientesCountAsync();
            if (pendientes > 0)
            {
                NotificationService.Warning(
                    "Operaciones pendientes",
                    $"Hay {pendientes} operación(es) sin sincronizar. Esperá a que se sincronicen antes de cerrar.");
                return;
            }

            var confirma = await MostrarConfirmacion("Cerrar el dia definitivamente?", "Esta accion NO se puede deshacer.\n\nUna vez cerrado:\n- No se pueden agregar mas operaciones a este dia\n- Los datos quedan bloqueados para auditoria");
            if (!confirma) return;

            try
            {
                var cierre = await _apiClient.CerrarDefinitivoAsync(_cierreId.Value);
                MostrarCierre(cierre);
                NotificationService.CierreCajaCompletado();
            }
            catch (Exception ex) { NotificationService.Error("Error al cerrar", ex.Message); }
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();

        private async void CargarOperacionesDiaAsync()
        {
            try
            {
                var hoy = DateTime.Today;
                var manana = hoy.AddDays(1);
                var response = await _apiClient.ObtenerOperacionesAsync(
                    desde: hoy, hasta: manana, pageSize: 200);
                dgOperacionesDia.ItemsSource = response.Items;
                txtCantidadOps.Text = $"({response.Items.Count})";
            }
            catch { }
        }

        private async System.Threading.Tasks.Task<bool> MostrarConfirmacion(string titulo, string mensaje)
        {
            var dialog = new Window { Title = titulo, Width = 450, Height = 220, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#161b22")) };
            var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
            panel.Children.Add(new TextBlock { Text = mensaje, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e6edf3")) });
            var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
            bool resultado = false;
            var btnSi = new Button { Content = "Si, cerrar definitivamente", Width = 180, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#da3633")), Foreground = Avalonia.Media.Brushes.White };
            var btnNo = new Button { Content = "Cancelar", Width = 100, Background = Avalonia.Media.Brushes.Transparent, Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e6edf3")) };
            btnSi.Click += (s, ev) => { resultado = true; dialog.Close(); };
            btnNo.Click += (s, ev) => { resultado = false; dialog.Close(); };
            btnPanel.Children.Add(btnSi); btnPanel.Children.Add(btnNo); panel.Children.Add(btnPanel); dialog.Content = panel;
            await dialog.ShowDialog(this);
            return resultado;
        }
    }
}
