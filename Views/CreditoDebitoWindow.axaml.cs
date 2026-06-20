using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using SistemaCambio.Views.Helpers;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaCambio.Views
{
    public partial class CreditoDebitoWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IOfflineOperacionService _offlineService;
        private List<CuentaDto> _todasLasCuentas = new();
        private List<MonedaDto> _monedasApi = new();
        private Control[] _orden = null!;
        private bool _actualizandoDesdeCombo;

        public CreditoDebitoWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            _offlineService = App.Services.GetRequiredService<IOfflineOperacionService>();

            InitializeComponent();
            _orden = [cmbMoneda, cmbCredito, txtImporteCredito, txtCotizacionCredito,
                      cmbDebito, txtImporteDebito, txtCotizacionDebito, txtObservaciones];
            AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();
            CargarDatosAsync();
        }

        // ── Carga inicial ────────────────────────────────────────────

        private async void CargarDatosAsync()
        {
            try
            {
                var cuentasTask = _apiClient.ObtenerCuentasAsync();
                var monedasTask = _apiClient.ObtenerMonedasAsync();
                await Task.WhenAll(cuentasTask, monedasTask);

                _todasLasCuentas = cuentasTask.Result;
                _monedasApi = monedasTask.Result;

                CargarCombos();
                await CargarClientesAsync();
            }
            catch (Exception ex)
            {
                NotificationService.Error("Error al cargar datos", ex.Message);
            }
        }

        private void CargarCombos()
        {
            // ── cmbMoneda: todas las monedas del catálogo ────────────
            cmbMoneda.Items.Clear();
            foreach (var m in _monedasApi.OrderBy(m => m.Codigo))
                cmbMoneda.Items.Add(new ComboBoxItem { Content = m.Codigo, Tag = m });

            // Seleccionar primera moneda (dispara CmbMoneda_SelectionChanged que llama RefrescarCombosCuentas)
            if (cmbMoneda.Items.Count > 0)
                cmbMoneda.SelectedIndex = 0;
        }

        private void RefrescarCombosCuentas(string moneda)
        {
            cmbCredito.Items.Clear();
            cmbDebito.Items.Clear();
            foreach (var cuenta in _todasLasCuentas
                         .Where(c => c.Tipo != "Externo")
                         .Where(c => CuentaFilter.PuedeOperarEnMoneda(c, moneda))
                         .OrderBy(c => c.Nombre))
            {
                var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = moneda, NombreCuenta = cuenta.Nombre };
                cmbCredito.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = tag });
                cmbDebito.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = tag });
            }
            if (cmbCredito.Items.Count > 0) cmbCredito.SelectedIndex = 0;
            if (cmbDebito.Items.Count > 0)  cmbDebito.SelectedIndex = 0;
        }

        private async Task CargarClientesAsync()
        {
            try
            {
                var clientes = await _apiClient.ObtenerClientesAsync();
                cmbCliente.Items.Clear();
                cmbCliente.Items.Add(new ComboBoxItem { Content = "(Sin cliente)", Tag = null });
                foreach (var cliente in clientes)
                    cmbCliente.Items.Add(new ComboBoxItem { Content = cliente.Nombre, Tag = cliente.Id });
                cmbCliente.SelectedIndex = 0;
            }
            catch (Exception ex) { AppLogger.Warn("CargarClientesAsync", ex); }
        }

        // ── Eventos ─────────────────────────────────────────────────

        private void CmbMoneda_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (cmbMoneda.SelectedItem is not ComboBoxItem { Tag: MonedaDto moneda }) return;
            _actualizandoDesdeCombo = true;
            txtMonedaDescripcion.Text = moneda.Nombre;
            _actualizandoDesdeCombo = false;
            RefrescarCombosCuentas(moneda.Codigo);
            _ = CargarCotizacionesDelDiaAsync(moneda.Codigo);
        }

        private void TxtMoneda_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_actualizandoDesdeCombo) return;
            var match = MonedaSearch.BuscarPorNombre(txtMonedaDescripcion.Text ?? "", _monedasApi);
            if (match == null) return;

            foreach (ComboBoxItem item in cmbMoneda.Items)
            {
                if (item.Tag is MonedaDto m && m.Codigo == match.Codigo)
                {
                    if (cmbMoneda.SelectedItem != item)
                        cmbMoneda.SelectedItem = item;
                    break;
                }
            }
        }

        private void TxtMoneda_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (cmbMoneda.SelectedItem is not ComboBoxItem { Tag: MonedaDto current }) return;
            var match = MonedaSearch.BuscarPorNombre(txtMonedaDescripcion.Text ?? "", _monedasApi);
            if (match == null)
            {
                _actualizandoDesdeCombo = true;
                txtMonedaDescripcion.Text = current.Nombre;
                _actualizandoDesdeCombo = false;
            }
        }

        private async Task CargarCotizacionesDelDiaAsync(string moneda)
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                var cot = cotizaciones.FirstOrDefault(c => c.CodigoMoneda == moneda);
                if (cot != null)
                {
                    txtCotizacionCredito.Text = cot.CotizacionCompra.ToString("N5");
                    txtCotizacionDebito.Text  = cot.CotizacionVenta.ToString("N5");
                }
                else
                {
                    txtCotizacionCredito.Text = "0.00000";
                    txtCotizacionDebito.Text  = "0.00000";
                }
                RecalcularCredito();
                RecalcularDebito();
            }
            catch (Exception ex) { AppLogger.Warn("CargarCotizacionesDelDiaAsync", ex); }
        }

        private void RecalcularCredito_KeyUp(object? sender, KeyEventArgs e) => RecalcularCredito();
        private void RecalcularDebito_KeyUp(object? sender, KeyEventArgs e)  => RecalcularDebito();

        private void RecalcularCredito()
        {
            try
            {
                decimal importe   = ParsearMonto(txtImporteCredito.Text);
                decimal cotizacion = ParsearMonto(txtCotizacionCredito.Text);
                decimal pesos      = Math.Round(importe * cotizacion, 2, MidpointRounding.AwayFromZero);
                txtPesosCredito.Text = pesos.ToString("N2");
            }
            catch (OverflowException)
            {
                txtPesosCredito.Text = "0,00";
            }
        }

        private void RecalcularDebito()
        {
            try
            {
                decimal importe   = ParsearMonto(txtImporteDebito.Text);
                decimal cotizacion = ParsearMonto(txtCotizacionDebito.Text);
                decimal pesos      = Math.Round(importe * cotizacion, 2, MidpointRounding.AwayFromZero);
                txtPesosDebito.Text = pesos.ToString("N2");
            }
            catch (OverflowException)
            {
                txtPesosDebito.Text = "0,00";
            }
        }

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is not TextBox tb) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (MontoHelper.Parsear(tb.Text) == 0)
                    tb.Clear();
                else
                    tb.SelectAll();
            });
        }

        public void TextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && string.IsNullOrWhiteSpace(tb.Text))
                tb.Text = "0";
        }

        // ── Siguiente: saltar a la pestaña Cliente ──────────────────

        private void BtnSiguiente_Click(object? sender, RoutedEventArgs e)
        {
            if (tabRoot.Items.Count > 1)
                tabRoot.SelectedIndex = 1;
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static decimal ParsearMonto(string? texto) => MontoHelper.Parsear(texto);

        private CuentaMonedaTag? ObtenerTagCredito()
            => (cmbCredito.SelectedItem as ComboBoxItem)?.Tag as CuentaMonedaTag;

        private CuentaMonedaTag? ObtenerTagDebito()
            => (cmbDebito.SelectedItem as ComboBoxItem)?.Tag as CuentaMonedaTag;

        // ── Validación ───────────────────────────────────────────────

        private bool ValidarCampos()
        {
            decimal importeCredito = ParsearMonto(txtImporteCredito.Text);
            decimal importeDebito  = ParsearMonto(txtImporteDebito.Text);
            if (importeCredito <= 0 && importeDebito <= 0)
            {
                NotificationService.Warning("Campo requerido", "Ingrese al menos un importe mayor a cero.");
                txtImporteCredito.Focus();
                return false;
            }
            if (ObtenerTagCredito() == null || ObtenerTagDebito() == null)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione las cuentas crédito y débito.");
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

        // ── Aceptar / Cancelar ───────────────────────────────────────

        private async void BtnAceptar_Click(object? sender, RoutedEventArgs e)
        {
            if (await EjecutarOperacion())
            {
                NotificationService.Success("Crédito/Débito registrado", "Operación completada");
                Close();
            }
        }

        private async Task<bool> EjecutarOperacion()
        {
            OcultarErrorServidor();
            if (!ValidarCampos()) return false;

            decimal importeCredito = ParsearMonto(txtImporteCredito.Text);
            decimal importeDebito  = ParsearMonto(txtImporteDebito.Text);

            var tagCredito = ObtenerTagCredito()!;
            var tagDebito  = ObtenerTagDebito()!;

            int? clienteId = null;
            if ((cmbCliente.SelectedItem as ComboBoxItem)?.Tag is int cId) clienteId = cId;

            var request = new CrearCreditoDebitoRequest
            {
                CuentaCreditoId = tagCredito.CuentaId,
                CuentaDebitoId  = tagDebito.CuentaId,
                MonedaCredito   = tagCredito.Moneda,
                MonedaDebito    = tagDebito.Moneda,
                MontoCredito    = importeCredito,
                MontoDebito     = importeDebito,
                Cotizacion      = ParsearMonto(txtCotizacionCredito.Text),
                ClienteId       = clienteId,
                Observaciones   = txtObservaciones.Text ?? ""
            };

            var resultado = await _offlineService.GuardarCreditoDebitoAsync(request);
            if (!resultado.Exitoso)
            {
                MostrarErrorServidor(resultado.Mensaje);
                return false;
            }
            return true;
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();

        // ── Navegación por teclado ───────────────────────────────────

        private void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
            if (e.Key != Key.Down && e.Key != Key.Up) return;
            if (e.Source is ComboBox cb && cb.IsDropDownOpen) return;
            if (e.Source is not (TextBox or ComboBox)) return;
            MoverFoco(e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
        }

        private void MoverFoco(int delta)
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            var idx = Array.IndexOf(_orden, focused);
            if (idx < 0) return;
            var next = idx + delta;
            if (next >= 0 && next < _orden.Length)
                _orden[next].Focus();
        }
    }
}
