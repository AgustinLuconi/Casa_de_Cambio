using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using SistemaCambio.ViewModels.Models;
using SistemaCambio.Views.Helpers;

namespace SistemaCambio.ViewModels
{
    public partial class VentaViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IOfflineOperacionService _offlineService;
        private readonly IDialogService _dialogService;

        private List<CuentaDto> _todasLasCuentas = new();
        private decimal _cotizacionDia;
        private bool _recalculando;

        [ObservableProperty] private List<MonedaDto> _monedasDisponibles = new();
        [ObservableProperty] private MonedaDto? _monedaSeleccionada;
        [ObservableProperty] private string _monedaNombre = "";

        [ObservableProperty] private List<CuentaMonedaTag> _cuentasAcreditar = new();
        [ObservableProperty] private List<CuentaMonedaTag> _cuentasDebitar = new();
        [ObservableProperty] private CuentaMonedaTag? _cuentaAcreditar;
        [ObservableProperty] private CuentaMonedaTag? _cuentaDebitar;

        [ObservableProperty] private string _monedaExtranjeraTexto = "0.00";
        [ObservableProperty] private string _cotizacionTexto = "0.00000";
        [ObservableProperty] private string _pesosTexto = "0.00";
        [ObservableProperty] private string _ingresaTexto = "0.00";
        [ObservableProperty] private string _vueltoTexto = "0.00";

        [ObservableProperty] private string? _observaciones;

        [ObservableProperty] private bool _mostrarError;
        [ObservableProperty] private string _mensajeError = "";

        public ICommand AceptarCommand { get; }

        public event Action<int, bool, string>? OperacionGuardada;
        public event Action? SolicitarCierre;

        public VentaViewModel(ICasaCambioApiClient apiClient, IOfflineOperacionService offlineService, IDialogService dialogService)
        {
            _apiClient = apiClient;
            _offlineService = offlineService;
            _dialogService = dialogService;
            AceptarCommand = new AsyncRelayCommand(AceptarAsync);
            _ = CargarDatosAsync();
        }

        // ── Carga inicial ────────────────────────────────────────────

        private async Task CargarDatosAsync()
        {
            try
            {
                var cuentasTask = _apiClient.ObtenerCuentasAsync();
                var monedasTask = _apiClient.ObtenerMonedasAsync();
                await Task.WhenAll(cuentasTask, monedasTask);

                _todasLasCuentas = cuentasTask.Result;
                MonedasDisponibles = monedasTask.Result.Where(m => m.Codigo != "ARS").OrderBy(m => m.Codigo).ToList();

                var tagsArs = CuentaAutoComplete.ConstruirTags(_todasLasCuentas, "ARS");
                CuentasAcreditar = tagsArs;
                CuentaAcreditar = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, "ARS", tagsArs);

                if (MonedasDisponibles.Count > 0)
                    MonedaSeleccionada = MonedasDisponibles[0];
                else
                    FiltrarCuentasDebitar(null);
            }
            catch (Exception ex) { NotificationService.Error("Error al cargar datos", ex.Message); }
        }

        partial void OnMonedaSeleccionadaChanged(MonedaDto? value)
        {
            MonedaNombre = value?.Nombre ?? "";
            FiltrarCuentasDebitar(value?.Codigo);
            if (value != null)
                _ = CargarCotizacionDelDiaAsync(value.Codigo);
        }

        private void FiltrarCuentasDebitar(string? monedaFiltro)
        {
            if (monedaFiltro == null)
            {
                CuentasDebitar = new();
                CuentaDebitar = null;
                return;
            }
            var tags = CuentaAutoComplete.ConstruirTags(_todasLasCuentas, monedaFiltro);
            CuentasDebitar = tags;
            // Auto-selección: primera caja Efectivo con saldo en la moneda (ej. EFECTIVO USD)
            CuentaDebitar = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, monedaFiltro, tags);
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
                    CotizacionTexto = cot.CotizacionVenta.ToString("N5");
                }
                else
                {
                    _cotizacionDia = 0;
                    CotizacionTexto = "0.00000";
                }
            }
            catch (Exception ex) { AppLogger.Warn("CargarCotizacionDelDiaAsync", ex); }
        }

        // ── Cálculo reactivo ─────────────────────────────────────────

        partial void OnMonedaExtranjeraTextoChanged(string value) => Recalcular();
        partial void OnCotizacionTextoChanged(string value) => Recalcular();
        partial void OnIngresaTextoChanged(string value) => Recalcular();

        private void Recalcular()
        {
            if (_recalculando) return;
            _recalculando = true;
            try
            {
                decimal monedaExtranjera = MontoHelper.Parsear(MonedaExtranjeraTexto);
                decimal cotizacion = MontoHelper.Parsear(CotizacionTexto);
                decimal pesos = Math.Round(monedaExtranjera * cotizacion, 2, MidpointRounding.AwayFromZero);
                PesosTexto = pesos.ToString("N2");

                decimal ingresa = MontoHelper.Parsear(IngresaTexto);
                VueltoTexto = (ingresa - pesos).ToString("N2");
            }
            catch (OverflowException)
            {
                PesosTexto = "0,00";
                VueltoTexto = "0,00";
            }
            finally { _recalculando = false; }
        }

        // ── Validación ───────────────────────────────────────────────

        private bool ValidarCampos()
        {
            if (MonedaSeleccionada == null)
            {
                NotificationService.Warning("Sin moneda", "Seleccione la moneda a vender.");
                return false;
            }
            decimal monedaExtranjera = MontoHelper.Parsear(MonedaExtranjeraTexto);
            if (monedaExtranjera <= 0)
            {
                NotificationService.Warning("Campo requerido", "Ingrese un monto en moneda extranjera mayor a cero.");
                return false;
            }
            decimal cotizacion = MontoHelper.Parsear(CotizacionTexto);
            if (cotizacion <= 0)
            {
                NotificationService.Warning("Campo requerido", "Ingrese una cotización válida.");
                return false;
            }
            if (CuentaDebitar == null)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione la cuenta de divisa a debitar.");
                return false;
            }
            if (CuentaAcreditar == null)
            {
                NotificationService.Warning("Sin cuenta ARS", "Seleccione la cuenta ARS a acreditar.");
                return false;
            }
            return true;
        }

        // ── Aceptar ──────────────────────────────────────────────────

        private async Task AceptarAsync()
        {
            MostrarError = false;
            MensajeError = "";

            if (!ValidarCampos()) return;

            decimal monedaExtranjera = MontoHelper.Parsear(MonedaExtranjeraTexto);
            decimal cotizacion = MontoHelper.Parsear(CotizacionTexto);
            decimal pesos = MontoHelper.Parsear(PesosTexto);
            var tagDebitar = CuentaDebitar!;
            var tagAcreditar = CuentaAcreditar!;

            // Confirmación explícita: evita que un Enter accidental dispare la operación sin revisar
            var confirmar = await _dialogService.ConfirmarAsync(
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
                    var continuar = await _dialogService.ConfirmarAsync(
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
                    var continuar = await _dialogService.ConfirmarAsync(
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
                var continuar = await _dialogService.ConfirmarAsync(
                    "Monto elevado",
                    $"El monto en ARS (${pesos:N2}) supera los $5.000.000.\n\n¿Desea continuar?",
                    "Continuar de todas formas");
                if (!continuar) return;
            }

            var request = new CrearOperacionRequest
            {
                CuentaOrigenId = tagDebitar.CuentaId,    // cuenta divisa que debita
                CuentaDestinoId = tagAcreditar.CuentaId, // caja ARS que acredita
                MonedaOrigen = tagDebitar.Moneda,
                MonedaDestino = "ARS",
                MontoOrigen = monedaExtranjera,
                MontoDestino = pesos,
                Cotizacion = cotizacion,
                Observaciones = Observaciones ?? "Venta de divisa"
            };

            try
            {
                var resultado = await _offlineService.GuardarVentaAsync(request);

                if (!resultado.Exitoso)
                {
                    MensajeError = resultado.Mensaje;
                    MostrarError = true;
                    return;
                }

                OperacionGuardada?.Invoke(resultado.OperacionId ?? 0, resultado.IsOffline, resultado.Mensaje);
                SolicitarCierre?.Invoke();
            }
            catch (Exception ex)
            {
                MensajeError = ex.Message;
                MostrarError = true;
            }
        }
    }
}
