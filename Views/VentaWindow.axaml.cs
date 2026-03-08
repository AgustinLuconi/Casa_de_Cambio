using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using CasaCambio.Shared.Requests;
using System;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class VentaWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly OfflineOperacionService _offlineService;

        public VentaWindow()
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
                foreach (var moneda in monedas)
                    cmbMoneda.Items.Add(new ComboBoxItem { Content = $"{moneda.Codigo} - {moneda.Nombre}", Tag = moneda.Codigo });
                if (cmbMoneda.Items.Count > 0) cmbMoneda.SelectedIndex = 0;

                var cuentas = await _apiClient.ObtenerCuentasAsync();
                cmbDebitar.Items.Clear();
                cmbAcreditar.Items.Clear();
                foreach (var cuenta in cuentas.OrderBy(c => c.Nombre))
                {
                    if (cuenta.Saldos.Any())
                    {
                        foreach (var saldo in cuenta.Saldos.OrderBy(s => s.Moneda))
                        {
                            var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = saldo.Moneda, NombreCuenta = cuenta.Nombre };
                            var texto = $"{cuenta.Nombre} ({saldo.Moneda})";
                            cmbDebitar.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                            cmbAcreditar.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                        }
                    }
                    else
                    {
                        var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = "ARS", NombreCuenta = cuenta.Nombre };
                        cmbDebitar.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                        cmbAcreditar.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                    }
                }
                if (cmbDebitar.Items.Count > 0) cmbDebitar.SelectedIndex = 0;
                if (cmbAcreditar.Items.Count > 0) cmbAcreditar.SelectedIndex = 0;

                await CargarCotizacionDelDiaAsync();
            }
            catch (Exception ex) { NotificationService.Error("Error al cargar datos", ex.Message); }
        }

        private void CmbMoneda_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _ = CargarCotizacionDelDiaAsync();
            ActualizarLabelsMoneda();
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
                if (cot != null) txtCotizacion.Text = cot.CotizacionVenta.ToString("N5");
            }
            catch { }
        }

        private decimal ParsearMonto(string? texto) => MontoHelper.Parsear(texto);

        private void Recalcular_KeyUp(object? sender, KeyEventArgs e)
        {
            decimal montoExtranjera = ParsearMonto(txtMontoExtranjera.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            txtPesos.Text = (montoExtranjera * cotizacion).ToString("N2");
        }

        private void CalcularVuelto_KeyUp(object? sender, KeyEventArgs e)
        {
            decimal pesos = ParsearMonto(txtPesos.Text);
            decimal ingresa = ParsearMonto(txtIngresa.Text);
            txtVuelto.Text = (ingresa - pesos).ToString("N2");
        }

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is TextBox textBox) textBox.SelectAll();
        }

        private async void BtnAceptar_Click(object? sender, RoutedEventArgs e)
        {
            decimal montoExtranjera = ParsearMonto(txtMontoExtranjera.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            decimal pesos = ParsearMonto(txtPesos.Text);

            if (montoExtranjera <= 0 || cotizacion <= 0) return;

            var itemDebitar = cmbDebitar.SelectedItem as ComboBoxItem;
            var itemAcreditar = cmbAcreditar.SelectedItem as ComboBoxItem;

            if (itemDebitar?.Tag is not CuentaMonedaTag tagDebitar || itemAcreditar?.Tag is not CuentaMonedaTag tagAcreditar)
            {
                NotificationService.Warning("Seleccion incompleta", "Seleccione las cuentas");
                return;
            }

            var request = new CrearOperacionRequest
            {
                CuentaOrigenId = tagDebitar.CuentaId,
                CuentaDestinoId = tagAcreditar.CuentaId,
                MonedaOrigen = tagDebitar.Moneda,
                MonedaDestino = tagAcreditar.Moneda,
                MontoOrigen = montoExtranjera,
                MontoDestino = pesos,
                Cotizacion = cotizacion,
                Observaciones = txtObservaciones.Text ?? ""
            };

            var resultado = await _offlineService.GuardarVentaAsync(request);

            if (!resultado.Exitoso)
            {
                NotificationService.Error("Error al guardar venta", resultado.Mensaje);
                return;
            }

            if (resultado.IsOffline)
                NotificationService.Warning("Guardada offline", resultado.Mensaje);
            else
                NotificationService.OperacionGuardada("Venta", resultado.OperacionId ?? 0);
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

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
