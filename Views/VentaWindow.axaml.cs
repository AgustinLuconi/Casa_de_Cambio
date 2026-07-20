using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using SistemaCambio.Views.Helpers;
using SistemaCambio.ViewModels.Models;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaCambio.Views
{
    public partial class VentaWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IOfflineOperacionService _offlineService;
        private decimal _cotizacionDia;
        private List<CuentaDto> _todasLasCuentas = new();
        private List<MonedaDto> _monedasApi = new();
        private Control[] _orden = null!;
        private bool _actualizandoDesdeCombo;

        public VentaWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            _offlineService = App.Services.GetRequiredService<IOfflineOperacionService>();

            InitializeComponent();
            CuentaAutoComplete.Configurar(cmbCuentaDebitar);
            CuentaAutoComplete.Configurar(cmbCuentaAcreditar);
            _orden = [cmbMoneda, txtMonedaExtranjera, cmbCuentaDebitar,
                      txtCotizacion, txtIngresa, cmbCuentaAcreditar,
                      txtObservaciones, cmbTipoOperacion, btnAceptar];
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
            }
            catch (Exception ex)
            {
                NotificationService.Error("Error al cargar datos", ex.Message);
            }
        }

        private void CargarCombos()
        {
            // ── cmbMoneda: todas las monedas activas del catálogo (no ARS) ──
            cmbMoneda.Items.Clear();
            foreach (var m in _monedasApi.Where(m => m.Codigo != "ARS").OrderBy(m => m.Codigo))
                cmbMoneda.Items.Add(new ComboBoxItem { Content = m.Codigo, Tag = m });

            // ── cmbCuentaAcreditar: todas las cuentas, preseleccionando la caja de pesos ──
            var tagsArs = CuentaAutoComplete.ConstruirTags(_todasLasCuentas, "ARS");
            cmbCuentaAcreditar.ItemsSource = tagsArs;
            CuentaAutoComplete.Seleccionar(cmbCuentaAcreditar,
                CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, "ARS", tagsArs));

            // Seleccionar primera moneda (dispara CmbMoneda_SelectionChanged)
            if (cmbMoneda.Items.Count > 0)
                cmbMoneda.SelectedIndex = 0;
            else
                FiltrarCuentasDebitar(null);
        }

        private void FiltrarCuentasDebitar(string? monedaFiltro)
        {
            if (monedaFiltro == null)
            {
                cmbCuentaDebitar.ItemsSource = new List<CuentaMonedaTag>();
                CuentaAutoComplete.Seleccionar(cmbCuentaDebitar, null);
                return;
            }
            var tags = CuentaAutoComplete.ConstruirTags(_todasLasCuentas, monedaFiltro);
            cmbCuentaDebitar.ItemsSource = tags;
            // Auto-selección: primera caja Efectivo con saldo en la moneda (ej. EFECTIVO USD)
            CuentaAutoComplete.Seleccionar(cmbCuentaDebitar,
                CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, monedaFiltro, tags));
        }

        // ── Eventos ─────────────────────────────────────────────────

        private void CmbMoneda_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (cmbMoneda.SelectedItem is not ComboBoxItem { Tag: MonedaDto moneda }) return;
            _actualizandoDesdeCombo = true;
            txtMonedaNombre.Text = moneda.Nombre;
            _actualizandoDesdeCombo = false;
            FiltrarCuentasDebitar(moneda.Codigo);
            _ = CargarCotizacionDelDiaAsync(moneda.Codigo);
        }

        private void TxtMoneda_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_actualizandoDesdeCombo) return;
            var disponibles = _monedasApi.Where(m => m.Codigo != "ARS");
            var match = MonedaSearch.BuscarPorNombre(txtMonedaNombre.Text ?? "", disponibles);
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
            var disponibles = _monedasApi.Where(m => m.Codigo != "ARS");
            var match = MonedaSearch.BuscarPorNombre(txtMonedaNombre.Text ?? "", disponibles);
            if (match == null)
            {
                _actualizandoDesdeCombo = true;
                txtMonedaNombre.Text = current.Nombre;
                _actualizandoDesdeCombo = false;
            }
        }

        private async Task CargarCotizacionDelDiaAsync(string moneda)
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                var cot = cotizaciones.FirstOrDefault(c => c.CodigoMoneda == moneda);
                if (cot != null)
                {
                    _cotizacionDia = cot.CotizacionVenta;
                    txtCotizacion.Text = cot.CotizacionVenta.ToString("N5");
                }
                else
                {
                    _cotizacionDia = 0;
                    txtCotizacion.Text = "0.00000";
                }
                Recalcular();
            }
            catch (Exception ex) { AppLogger.Warn("CargarCotizacionDelDiaAsync", ex); }
        }

        private void Recalcular_KeyUp(object? sender, KeyEventArgs e) => Recalcular();

        private void Recalcular()
        {
            try
            {
                decimal monedaExtranjera = ParsearMonto(txtMonedaExtranjera.Text);
                decimal cotizacion       = ParsearMonto(txtCotizacion.Text);
                decimal pesos            = Math.Round(monedaExtranjera * cotizacion, 2, MidpointRounding.AwayFromZero);
                txtPesos.Text  = pesos.ToString("N2");

                decimal ingresa = ParsearMonto(txtIngresa.Text);
                txtVuelto.Text = (ingresa - pesos).ToString("N2");
            }
            catch (OverflowException)
            {
                txtPesos.Text = "0,00";
                txtVuelto.Text = "0,00";
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

        // ── Helpers de selección ────────────────────────────────────

        private static decimal ParsearMonto(string? texto) => MontoHelper.Parsear(texto);

        private CuentaMonedaTag? ObtenerTagDebitar()
            => CuentaAutoComplete.ObtenerSeleccion(cmbCuentaDebitar);

        private CuentaMonedaTag? ObtenerTagAcreditar()
            => CuentaAutoComplete.ObtenerSeleccion(cmbCuentaAcreditar);

        // ── Validación ───────────────────────────────────────────────

        private bool ValidarCampos()
        {
            if (cmbMoneda.SelectedItem == null)
            {
                NotificationService.Warning("Sin moneda", "Seleccione la moneda a vender.");
                return false;
            }
            decimal monedaExtranjera = ParsearMonto(txtMonedaExtranjera.Text);
            if (monedaExtranjera <= 0)
            {
                NotificationService.Warning("Campo requerido", "Ingrese un monto en moneda extranjera mayor a cero.");
                txtMonedaExtranjera.Focus();
                return false;
            }
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            if (cotizacion <= 0)
            {
                NotificationService.Warning("Campo requerido", "Ingrese una cotización válida.");
                txtCotizacion.Focus();
                return false;
            }
            if (ObtenerTagDebitar() == null)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione la cuenta de divisa a debitar.");
                return false;
            }
            if (ObtenerTagAcreditar() == null)
            {
                NotificationService.Warning("Sin cuenta ARS", "Seleccione la cuenta ARS a acreditar.");
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
            OcultarErrorServidor();
            if (!ValidarCampos()) return;

            decimal monedaExtranjera = ParsearMonto(txtMonedaExtranjera.Text);
            decimal cotizacion       = ParsearMonto(txtCotizacion.Text);
            decimal pesos            = ParsearMonto(txtPesos.Text);
            var tagDebitar           = ObtenerTagDebitar()!;
            var tagAcreditar         = ObtenerTagAcreditar()!;

            // Confirmación explícita: evita que un Enter accidental dispare la operación sin revisar
            var confirmar = await DialogHelper.ConfirmarAsync(this,
                "Confirmar Venta",
                $"Vender {monedaExtranjera:N2} {tagDebitar.Moneda} a {cotizacion:N5}\n" +
                $"Debita de: {tagDebitar.NombreCuenta}\n" +
                $"Acredita ${pesos:N2} en: {tagAcreditar.NombreCuenta}\n\n¿Confirmar la operación?",
                "Confirmar");
            if (!confirmar) return;

            // Advertencia cotización inusual (>5% del precio del día)
            if (_cotizacionDia > 0)
            {
                decimal diffPct = Math.Abs(cotizacion - _cotizacionDia) / _cotizacionDia * 100;
                if (diffPct > AppConstants.CotizacionDiffPctUmbral)
                {
                    var continuar = await DialogHelper.ConfirmarAsync(this,
                        "Cotización inusual",
                        $"La cotización ingresada ({cotizacion:N5}) difiere un {diffPct:N1}% de la cotización del día ({_cotizacionDia:N5}).\n\n¿Desea continuar?",
                        "Continuar de todas formas");
                    if (!continuar) return;
                }
            }

            // Advertencia PPP — validar si la venta es rentable
            try
            {
                var ppp = await _apiClient.ValidarVentaPPPAsync(tagDebitar.Moneda, cotizacion);
                if (!ppp.EsRentable)
                {
                    var continuar = await DialogHelper.ConfirmarAsync(this,
                        "Venta por debajo del PPP",
                        $"Está vendiendo {tagDebitar.Moneda} a {cotizacion:N5} pero su costo promedio (PPP) es {ppp.PPP:N5}.\n" +
                        $"Pérdida estimada: ${Math.Abs(ppp.Ganancia):N2} por unidad.\n\n¿Desea continuar de todas formas?",
                        "Continuar de todas formas");
                    if (!continuar) return;
                }
            }
            catch (Exception ex) { AppLogger.Warn("BtnAceptar_Click.PPP", ex); }

            // Advertencia monto elevado
            if (pesos > AppConstants.MontoAltoARS)
            {
                var continuar = await DialogHelper.ConfirmarAsync(this,
                    "Monto elevado",
                    $"El monto en ARS (${pesos:N2}) supera los $5.000.000.\n\n¿Desea continuar?",
                    "Continuar de todas formas");
                if (!continuar) return;
            }

            var request = new CrearOperacionRequest
            {
                CuentaOrigenId  = tagDebitar.CuentaId,    // cuenta divisa que debita
                CuentaDestinoId = tagAcreditar.CuentaId,  // caja ARS que acredita
                MonedaOrigen    = tagDebitar.Moneda,
                MonedaDestino   = "ARS",
                MontoOrigen     = monedaExtranjera,
                MontoDestino    = pesos,
                Cotizacion      = cotizacion,
                Observaciones   = txtObservaciones.Text ?? "Venta de divisa"
            };

            var resultado = await _offlineService.GuardarVentaAsync(request);

            if (!resultado.Exitoso)
            {
                MostrarErrorServidor(resultado.Mensaje);
                return;
            }

            if (resultado.IsOffline)
                NotificationService.Warning("Guardada offline", resultado.Mensaje);
            else
                NotificationService.OperacionGuardada("Venta", resultado.OperacionId ?? 0);

            Close();
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();

        // ── Navegación por teclado ───────────────────────────────────

        private void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (e.Source is Control esc &&
                    esc.FindAncestorOfType<AutoCompleteBox>(includeSelf: true) is { IsDropDownOpen: true } acbEsc)
                { acbEsc.IsDropDownOpen = false; e.Handled = true; return; }
                Close(); e.Handled = true; return;
            }
            // Enter avanza al siguiente campo (como Tab/flecha abajo) en vez de disparar Aceptar directo,
            // para que un Enter apretado sin querer no envíe la operación sin revisar.
            if (e.Key != Key.Down && e.Key != Key.Up && e.Key != Key.Enter) return;
            if (e.Source is ComboBox cb && cb.IsDropDownOpen) return;
            if (e.Source is Control c &&
                c.FindAncestorOfType<AutoCompleteBox>(includeSelf: true) is { IsDropDownOpen: true }) return;
            if (e.Source is not (TextBox or ComboBox)) return;
            MoverFoco(e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
        }

        private void MoverFoco(int delta)
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            // El foco real puede estar en el TextBox interno de un AutoCompleteBox
            Control? actual = focused is not null && Array.IndexOf(_orden, focused) >= 0
                ? focused
                : focused?.FindAncestorOfType<AutoCompleteBox>(includeSelf: true);
            var idx = Array.IndexOf(_orden, actual);
            if (idx < 0) return;
            var next = idx + delta;
            if (next >= 0 && next < _orden.Length)
                _orden[next].Focus();
        }
    }
}
