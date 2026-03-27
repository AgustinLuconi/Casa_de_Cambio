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
        private decimal _cotizacionDia;
        private CuentaMonedaTag? _cuentaARSFija;

        public CompraWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            _offlineService = App.Services.GetRequiredService<OfflineOperacionService>();

            InitializeComponent();
            cmbDestino.SelectionChanged += CmbDestino_SelectionChanged;
            CargarDatosAsync();
        }

        private async void CargarDatosAsync()
        {
            try
            {
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                CargarComboCuentas(cuentas);
            }
            catch (Exception ex)
            {
                NotificationService.Error("Error al cargar datos", ex.Message);
            }
        }

        private void CargarComboCuentas(System.Collections.Generic.List<CuentaDto> cuentas)
        {
            cmbDestino.Items.Clear();
            _cuentaARSFija = null;

            foreach (var cuenta in cuentas.OrderBy(c => c.Nombre))
            {
                // Buscar cuenta ARS fija: primera Efectivo con saldo ARS
                if (_cuentaARSFija == null && cuenta.Tipo == "Efectivo")
                {
                    var saldoARS = cuenta.Saldos.FirstOrDefault(s => s.Moneda == "ARS");
                    if (saldoARS != null)
                        _cuentaARSFija = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = "ARS", NombreCuenta = cuenta.Nombre };
                }

                // cmbDestino: solo cuentas con moneda ≠ ARS
                foreach (var saldo in cuenta.Saldos.Where(s => s.Moneda != "ARS").OrderBy(s => s.Moneda))
                {
                    var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = saldo.Moneda, NombreCuenta = cuenta.Nombre };
                    cmbDestino.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} ({saldo.Moneda})", Tag = tag });
                }
            }

            if (_cuentaARSFija != null)
                txtCuentaARSFija.Text = $"{_cuentaARSFija.NombreCuenta} (ARS)";
            else
                txtCuentaARSFija.Text = "No se encontró cuenta Efectivo con ARS";

            if (cmbDestino.Items.Count > 0) cmbDestino.SelectedIndex = 0;
        }

        private void CmbDestino_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (cmbDestino.SelectedItem is ComboBoxItem item && item.Tag is CuentaMonedaTag tag)
                _ = CargarCotizacionDelDiaAsync(tag.Moneda);
        }

        private async System.Threading.Tasks.Task CargarCotizacionDelDiaAsync(string moneda)
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                var cot = cotizaciones.FirstOrDefault(c => c.CodigoMoneda == moneda);
                if (cot != null)
                {
                    _cotizacionDia = cot.CotizacionCompra;
                    txtCotizacion.Text = cot.CotizacionCompra.ToString("N5");
                }
                else
                {
                    _cotizacionDia = 0;
                    txtCotizacion.Text = "0.00000";
                }
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

        private bool ValidarCampos()
        {
            decimal montoDestino = ParsearMonto(txtMontoDestino.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);

            if (montoDestino <= 0)
            {
                NotificationService.Warning("Campo requerido", "Ingrese un monto destino mayor a cero.");
                txtMontoDestino.Focus();
                return false;
            }
            if (cotizacion <= 0)
            {
                NotificationService.Warning("Campo requerido", "Ingrese una cotización válida.");
                txtCotizacion.Focus();
                return false;
            }
            if (_cuentaARSFija == null)
            {
                NotificationService.Warning("Sin cuenta ARS", "No se encontró una cuenta Efectivo con saldo ARS.");
                return false;
            }
            var itemDestino = cmbDestino.SelectedItem as ComboBoxItem;
            if (itemDestino?.Tag is not CuentaMonedaTag)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione la cuenta destino para la divisa.");
                return false;
            }
            return true;
        }

        private void MostrarErrorServidor(string mensaje)
        {
            borderError.IsVisible = true;
            txtErrorServidor.Text = mensaje;
        }

        private void OcultarErrorServidor()
        {
            borderError.IsVisible = false;
            txtErrorServidor.Text = "";
        }

        private async void BotonAceptar_Click(object? sender, RoutedEventArgs e)
        {
            OcultarErrorServidor();
            if (!ValidarCampos()) return;

            decimal montoDestino = ParsearMonto(txtMontoDestino.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            decimal montoOrigen = ParsearMonto(txtMontoOrigen.Text);

            // Warning cotización inusual (>5% de diferencia con la del día)
            if (_cotizacionDia > 0)
            {
                decimal diffPct = Math.Abs(cotizacion - _cotizacionDia) / _cotizacionDia * 100;
                if (diffPct > 5)
                {
                    var continuar = await MostrarConfirmacion(
                        "Cotización inusual",
                        $"La cotización ingresada ({cotizacion:N5}) difiere un {diffPct:N1}% de la cotización del día ({_cotizacionDia:N5}).\n\n¿Desea continuar?");
                    if (!continuar) return;
                }
            }

            // Warning monto alto (>5.000.000 ARS)
            if (montoOrigen > 5_000_000m)
            {
                var continuar = await MostrarConfirmacion(
                    "Monto elevado",
                    $"El monto en ARS (${montoOrigen:N2}) supera los $5.000.000.\n\n¿Desea continuar?");
                if (!continuar) return;
            }

            var itemDestino = cmbDestino.SelectedItem as ComboBoxItem;
            var tagDestino = (CuentaMonedaTag)itemDestino!.Tag!;

            var request = new CrearOperacionRequest
            {
                CuentaOrigenId = _cuentaARSFija!.CuentaId,
                CuentaDestinoId = tagDestino.CuentaId,
                MonedaOrigen = "ARS",
                MonedaDestino = tagDestino.Moneda,
                MontoOrigen = montoOrigen,
                MontoDestino = montoDestino,
                Cotizacion = cotizacion,
                Observaciones = txtObservaciones.Text ?? "Compra de divisa"
            };

            var resultado = await _offlineService.GuardarCompraAsync(request);

            if (!resultado.Exitoso)
            {
                MostrarErrorServidor(resultado.Mensaje);
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
