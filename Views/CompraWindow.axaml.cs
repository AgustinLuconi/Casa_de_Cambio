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
    public class CuentaMonedaTag
    {
        public int CuentaId { get; set; }
        public string Moneda { get; set; } = "";
        public string NombreCuenta { get; set; } = "";
    }

    public partial class CompraWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IOfflineOperacionService _offlineService;
        private decimal _cotizacionDia;
        private List<CuentaDto> _todasLasCuentas = new();
        private List<MonedaDto> _monedasApi = new();

        public CompraWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            _offlineService = App.Services.GetRequiredService<IOfflineOperacionService>();

            InitializeComponent();
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
            // ── cmbMoneda: monedas disponibles (no ARS) ──────────────
            var monedasEnCuentas = _todasLasCuentas
                .SelectMany(c => c.Saldos.Where(s => s.Moneda != "ARS").Select(s => s.Moneda))
                .Distinct()
                .ToHashSet();

            cmbMoneda.Items.Clear();
            foreach (var m in _monedasApi.Where(m => m.Codigo != "ARS" && monedasEnCuentas.Contains(m.Codigo))
                                          .OrderBy(m => m.Codigo))
            {
                cmbMoneda.Items.Add(new ComboBoxItem { Content = m.Codigo, Tag = m });
            }
            // Monedas que están en cuentas pero no en el catálogo (fallback)
            foreach (var codigo in monedasEnCuentas.Where(c => c != "ARS" && !_monedasApi.Any(m => m.Codigo == c)).OrderBy(c => c))
                cmbMoneda.Items.Add(new ComboBoxItem { Content = codigo, Tag = new MonedaDto { Codigo = codigo, Nombre = codigo } });

            // ── cmbCuentaDebita: cajas Efectivo con saldo ARS ────────
            cmbCuentaDebita.Items.Clear();
            foreach (var cuenta in _todasLasCuentas.Where(c => c.Tipo == "Efectivo").OrderBy(c => c.Nombre))
            {
                if (cuenta.Saldos.Any(s => s.Moneda == "ARS"))
                {
                    var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = "ARS", NombreCuenta = cuenta.Nombre };
                    cmbCuentaDebita.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = tag });
                }
            }
            if (cmbCuentaDebita.Items.Count > 0) cmbCuentaDebita.SelectedIndex = 0;

            // Seleccionar primera moneda (dispara CmbMoneda_SelectionChanged)
            if (cmbMoneda.Items.Count > 0)
                cmbMoneda.SelectedIndex = 0;
            else
                FiltrarCuentasAcredita(null);
        }

        private void FiltrarCuentasAcredita(string? monedaFiltro)
        {
            cmbCuentaAcredita.Items.Clear();
            foreach (var cuenta in _todasLasCuentas.OrderBy(c => c.Nombre))
            {
                var saldos = cuenta.Saldos.Where(s => s.Moneda != "ARS");
                if (monedaFiltro != null)
                    saldos = saldos.Where(s => s.Moneda == monedaFiltro);

                foreach (var saldo in saldos.OrderBy(s => s.Moneda))
                {
                    var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = saldo.Moneda, NombreCuenta = cuenta.Nombre };
                    cmbCuentaAcredita.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = tag });
                }
            }
            if (cmbCuentaAcredita.Items.Count > 0) cmbCuentaAcredita.SelectedIndex = 0;
        }

        // ── Eventos ─────────────────────────────────────────────────

        private void CmbMoneda_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (cmbMoneda.SelectedItem is not ComboBoxItem { Tag: MonedaDto moneda }) return;
            txtMonedaNombre.Text = moneda.Nombre;
            FiltrarCuentasAcredita(moneda.Codigo);
            _ = CargarCotizacionDelDiaAsync(moneda.Codigo);
        }

        private async Task CargarCotizacionDelDiaAsync(string moneda)
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
                Recalcular();
            }
            catch (Exception ex) { AppLogger.Warn("CargarCotizacionDelDiaAsync", ex); }
        }

        private void Recalcular_KeyUp(object? sender, KeyEventArgs e) => Recalcular();

        private void Recalcular()
        {
            decimal monedaExtranjera = ParsearMonto(txtMonedaExtranjera.Text);
            decimal cotizacion       = ParsearMonto(txtCotizacion.Text);
            decimal pesos            = Math.Round(monedaExtranjera * cotizacion, 2, MidpointRounding.AwayFromZero);
            txtPesos.Text  = pesos.ToString("N2");

            decimal ingresa = ParsearMonto(txtIngresa.Text);
            txtVuelto.Text = (ingresa - pesos).ToString("N2");
        }

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is TextBox tb) tb.SelectAll();
        }

        // ── Helpers de selección ────────────────────────────────────

        private static decimal ParsearMonto(string? texto) => MontoHelper.Parsear(texto);

        private CuentaMonedaTag? ObtenerTagAcredita()
            => (cmbCuentaAcredita.SelectedItem as ComboBoxItem)?.Tag as CuentaMonedaTag;

        private CuentaMonedaTag? ObtenerTagDebita()
            => (cmbCuentaDebita.SelectedItem as ComboBoxItem)?.Tag as CuentaMonedaTag;

        // ── Validación ───────────────────────────────────────────────

        private bool ValidarCampos()
        {
            if (cmbMoneda.SelectedItem == null)
            {
                NotificationService.Warning("Sin moneda", "Seleccione la moneda a comprar.");
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
            if (ObtenerTagAcredita() == null)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione la cuenta donde se acreditará la divisa.");
                return false;
            }
            if (ObtenerTagDebita() == null)
            {
                NotificationService.Warning("Sin cuenta ARS", "Seleccione la cuenta ARS a debitar.");
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

        private async void BotonAceptar_Click(object? sender, RoutedEventArgs e)
        {
            OcultarErrorServidor();
            if (!ValidarCampos()) return;

            decimal monedaExtranjera = ParsearMonto(txtMonedaExtranjera.Text);
            decimal cotizacion       = ParsearMonto(txtCotizacion.Text);
            decimal pesos            = ParsearMonto(txtPesos.Text);
            var tagAcredita          = ObtenerTagAcredita()!;
            var tagDebita            = ObtenerTagDebita()!;

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
                CuentaOrigenId  = tagDebita.CuentaId,    // caja ARS que debita
                CuentaDestinoId = tagAcredita.CuentaId,  // cuenta divisa que acredita
                MonedaOrigen    = "ARS",
                MonedaDestino   = tagAcredita.Moneda,
                MontoOrigen     = pesos,
                MontoDestino    = monedaExtranjera,
                Cotizacion      = cotizacion,
                Observaciones   = txtObservaciones.Text ?? "Compra de divisa"
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

        private void BotonCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
