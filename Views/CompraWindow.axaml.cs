using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using System;
using System.Linq;

namespace SistemaCambio.Views
{
    public class CuentaMonedaTag
    {
        public int CuentaId { get; set; }
        public string Moneda { get; set; } = "";
        public string NombreCuenta { get; set; } = "";
    }

    public partial class CompraWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly OfflineOperacionService _offlineService;

        public CompraWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            _offlineService = App.Services.GetRequiredService<OfflineOperacionService>();

            InitializeComponent();
            CargarDatosAsync();
        }

        private async void CargarDatosAsync()
        {
            try
            {
                var monedas = await _apiClient.ObtenerMonedasAsync();
                cmbMoneda.Items.Clear();
                foreach (var moneda in monedas)
                    cmbMoneda.Items.Add(new ComboBoxItem { Content = $"{moneda.Codigo} - {moneda.Nombre}", Tag = moneda.Codigo });
                if (cmbMoneda.Items.Count > 0) cmbMoneda.SelectedIndex = 0;

                var cuentas = await _apiClient.ObtenerCuentasAsync();
                CargarComboCuentas(cuentas);
                await CargarCotizacionDelDiaAsync();
            }
            catch (Exception ex)
            {
                NotificationService.Error("Error al cargar datos", ex.Message);
            }
        }

        private void CargarComboCuentas(System.Collections.Generic.List<CuentaDto> cuentas)
        {
            cmbDestino.Items.Clear();
            cmbOrigen.Items.Clear();
            foreach (var cuenta in cuentas.OrderBy(c => c.Nombre))
            {
                if (cuenta.Saldos.Any())
                {
                    foreach (var saldo in cuenta.Saldos.OrderBy(s => s.Moneda))
                    {
                        var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = saldo.Moneda, NombreCuenta = cuenta.Nombre };
                        var texto = $"{cuenta.Nombre} ({saldo.Moneda})";
                        cmbDestino.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                        cmbOrigen.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                    }
                }
                else
                {
                    var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = "ARS", NombreCuenta = cuenta.Nombre };
                    cmbDestino.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                    cmbOrigen.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                }
            }
            if (cmbDestino.Items.Count > 0) cmbDestino.SelectedIndex = 0;
            if (cmbOrigen.Items.Count > 0) cmbOrigen.SelectedIndex = 0;
        }

        private void CmbMoneda_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _ = CargarCotizacionDelDiaAsync();
        }

        private string ObtenerCodigoMonedaSeleccionada()
        {
            if (cmbMoneda.SelectedItem is ComboBoxItem item)
            {
                var content = item.Content?.ToString() ?? "";
                if (content.Contains(" - ")) return content.Split(" - ")[0].Trim();
            }
            return "USD";
        }

        private void ActualizarLabelsMoneda() { }

        private async System.Threading.Tasks.Task CargarCotizacionDelDiaAsync()
        {
            try
            {
                var codigo = ObtenerCodigoMonedaSeleccionada();
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                var cot = cotizaciones.FirstOrDefault(c => c.CodigoMoneda == codigo);
                if (cot != null) txtCotizacion.Text = cot.CotizacionCompra.ToString("N5");
            }
            catch { }
        }

        private decimal ParsearMonto(string? texto) => MontoHelper.Parsear(texto);

        private void Recalcular_KeyUp(object? sender, KeyEventArgs e)
        {
            decimal montoDestino = ParsearMonto(txtMontoDestino.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            txtMontoOrigen.Text = (montoDestino * cotizacion).ToString("N2");
            CalcularVuelto();
        }

        private void CalcularVuelto()
        {
            decimal montoOrigen = ParsearMonto(txtMontoOrigen.Text);
            decimal pagaCon = ParsearMonto(txtPagaCon.Text);
            txtVuelto.Text = (pagaCon - montoOrigen).ToString("N2");
        }

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is TextBox textBox) textBox.SelectAll();
        }

        private async void BotonAceptar_Click(object? sender, RoutedEventArgs e)
        {
            decimal montoDestino = ParsearMonto(txtMontoDestino.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            decimal montoOrigen = ParsearMonto(txtMontoOrigen.Text);

            if (montoDestino <= 0 || cotizacion <= 0) return;

            var itemOrigen = cmbOrigen.SelectedItem as ComboBoxItem;
            var itemDestino = cmbDestino.SelectedItem as ComboBoxItem;

            if (itemOrigen?.Tag is not CuentaMonedaTag tagOrigen || itemDestino?.Tag is not CuentaMonedaTag tagDestino)
            {
                NotificationService.Warning("Seleccion incompleta", "Seleccione las cuentas");
                return;
            }

            var request = new CrearOperacionRequest
            {
                CuentaOrigenId = tagOrigen.CuentaId,
                CuentaDestinoId = tagDestino.CuentaId,
                MonedaOrigen = tagOrigen.Moneda,
                MonedaDestino = tagDestino.Moneda,
                MontoOrigen = montoOrigen,
                MontoDestino = montoDestino,
                Cotizacion = cotizacion,
                Observaciones = txtObservaciones.Text ?? "Compra de divisa"
            };

            var resultado = await _offlineService.GuardarCompraAsync(request);

            if (!resultado.Exitoso)
            {
                NotificationService.Error("Error al guardar compra", resultado.Mensaje);
                return;
            }

            if (resultado.IsOffline)
                NotificationService.Warning("Guardada offline", resultado.Mensaje);
            else
                NotificationService.OperacionGuardada("Compra", resultado.OperacionId ?? 0);
            Close();
        }

        private async System.Threading.Tasks.Task<bool> MostrarConfirmacion(string titulo, string mensaje)
        {
            var dialog = new Window { Title = titulo, Width = 480, Height = 220, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
            panel.Children.Add(new TextBlock { Text = mensaje, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 440 });
            var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, Margin = new Avalonia.Thickness(0, 15, 0, 0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            bool continuar = false;
            var btnContinuar = new Button { Content = "Continuar de todas formas" };
            var btnCancelar = new Button { Content = "Cancelar" };
            btnContinuar.Click += (s, ev) => { continuar = true; dialog.Close(); };
            btnCancelar.Click += (s, ev) => dialog.Close();
            btnPanel.Children.Add(btnContinuar);
            btnPanel.Children.Add(btnCancelar);
            panel.Children.Add(btnPanel);
            dialog.Content = panel;
            await dialog.ShowDialog(this);
            return continuar;
        }

        private void BotonCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
