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
    public partial class CompraViewModel : ViewModelBase
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

        [ObservableProperty] private List<CuentaMonedaTag> _cuentasAcredita = new();
        [ObservableProperty] private List<CuentaMonedaTag> _cuentasDebita = new();
        [ObservableProperty] private CuentaMonedaTag? _cuentaAcredita;
        [ObservableProperty] private CuentaMonedaTag? _cuentaDebita;

        [ObservableProperty] private string _monedaExtranjeraTexto = "0.00";
        [ObservableProperty] private string _cotizacionTexto = "0.00000";
        [ObservableProperty] private string _pesosTexto = "0.00";
        [ObservableProperty] private string _ingresaTexto = "0.00";
        [ObservableProperty] private string _vueltoTexto = "0.00";

        [ObservableProperty] private string _observaciones = "";

        [ObservableProperty] private bool _mostrarError;
        [ObservableProperty] private string _mensajeError = "";

        public ICommand AceptarCommand { get; }

        public event Action<int, bool, string>? OperacionGuardada;
        public event Action? SolicitarCierre;

        public CompraViewModel(ICasaCambioApiClient apiClient, IOfflineOperacionService offlineService, IDialogService dialogService)
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
                CuentasDebita = tagsArs;
                CuentaDebita = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, "ARS", tagsArs);

                if (MonedasDisponibles.Count > 0)
                    MonedaSeleccionada = MonedasDisponibles[0];
                else
                    FiltrarCuentasAcredita(null);
            }
            catch (Exception ex) { NotificationService.Error("Error al cargar datos", ex.Message); }
        }

        partial void OnMonedaSeleccionadaChanged(MonedaDto? value)
        {
            MonedaNombre = value?.Nombre ?? "";
            FiltrarCuentasAcredita(value?.Codigo);
            if (value != null)
                _ = CargarCotizacionDelDiaAsync(value.Codigo);
        }

        private void FiltrarCuentasAcredita(string? monedaFiltro)
        {
            if (monedaFiltro == null)
            {
                CuentasAcredita = new();
                CuentaAcredita = null;
                return;
            }
            var tags = CuentaAutoComplete.ConstruirTags(_todasLasCuentas, monedaFiltro);
            CuentasAcredita = tags;
            // Auto-selección: primera caja Efectivo con saldo en la moneda (ej. EFECTIVO USD)
            CuentaAcredita = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, monedaFiltro, tags);
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
                    CotizacionTexto = cot.CotizacionCompra.ToString("N5");
                }
                else
                {
                    _cotizacionDia = 0;
                    CotizacionTexto = "0.00000";
                }
                Recalcular();
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
                NotificationService.Warning("Sin moneda", "Seleccione la moneda a comprar.");
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
            if (CuentaAcredita == null)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione la cuenta donde se acreditará la divisa.");
                return false;
            }
            if (CuentaDebita == null)
            {
                NotificationService.Warning("Sin cuenta ARS", "Seleccione la cuenta ARS a debitar.");
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
            var tagAcredita = CuentaAcredita!;
            var tagDebita = CuentaDebita!;

            // Confirmación explícita: evita que un Enter accidental dispare la operación sin revisar
            var confirmar = await _dialogService.ConfirmarAsync(
                "Confirmar Compra",
                $"Comprar {monedaExtranjera:N2} {tagAcredita.Moneda} a {cotizacion:N5}\n" +
                $"Acredita en: {tagAcredita.NombreCuenta}\n" +
                $"Debita ${pesos:N2} de: {tagDebita.NombreCuenta}\n\n¿Confirmar la operación?",
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
                CuentaOrigenId = tagDebita.CuentaId,    // caja ARS que debita
                CuentaDestinoId = tagAcredita.CuentaId, // cuenta divisa que acredita
                MonedaOrigen = "ARS",
                MonedaDestino = tagAcredita.Moneda,
                MontoOrigen = pesos,
                MontoDestino = monedaExtranjera,
                Cotizacion = cotizacion,
                Observaciones = string.IsNullOrEmpty(Observaciones) ? "Compra de divisa" : Observaciones
            };

            try
            {
                var resultado = await _offlineService.GuardarCompraAsync(request);

                if (!resultado.Exitoso)
                {
                    MensajeError = resultado.Mensaje;
                    MostrarError = true;
                    return;
                }

                if (resultado.IsOffline)
                    NotificationService.Warning("Guardada offline", resultado.Mensaje);
                else
                    NotificationService.OperacionGuardada("Compra", resultado.OperacionId ?? 0);

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
