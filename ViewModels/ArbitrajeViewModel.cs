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
    public partial class ArbitrajeViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IOfflineOperacionService _offlineService;
        private readonly IDialogService _dialogService;
        private List<CuentaDto> _todasLasCuentas = new();
        private bool _recalculandoCompra;
        private bool _recalculandoVenta;

        [ObservableProperty] private List<MonedaDto> _monedasDisponibles = new();
        [ObservableProperty] private MonedaDto? _monedaCompra;
        [ObservableProperty] private MonedaDto? _monedaVenta;

        [ObservableProperty] private List<CuentaMonedaTag> _cuentasCompra = new();
        [ObservableProperty] private List<CuentaMonedaTag> _cuentasVenta = new();
        [ObservableProperty] private CuentaMonedaTag? _cuentaAcreditaCompra;
        [ObservableProperty] private CuentaMonedaTag? _cuentaDebitaVenta;

        [ObservableProperty] private string _montoExtranjeroCompraTexto = "0.00";
        [ObservableProperty] private string _cotizacionCompraTexto = "0.00000";
        [ObservableProperty] private string _pesosCompraTexto = "0.00";

        [ObservableProperty] private string _montoExtranjeroVentaTexto = "0.00";
        [ObservableProperty] private string _cotizacionVentaTexto = "0.00000";
        [ObservableProperty] private string _pesosVentaTexto = "0.00";

        [ObservableProperty] private string _observaciones = "";
        [ObservableProperty] private string _tipoOperacion = "CLIENTE";
        public List<string> TiposOperacion { get; } = new() { "CLIENTE", "CASA" };

        [ObservableProperty] private bool _mostrarError;
        [ObservableProperty] private string _mensajeError = "";

        public ICommand AceptarCommand { get; }

        public event Action<int, int, bool, string>? OperacionGuardada;
        public event Action? SolicitarCierre;

        public ArbitrajeViewModel(ICasaCambioApiClient apiClient, IOfflineOperacionService offlineService, IDialogService dialogService)
        {
            _apiClient = apiClient;
            _offlineService = offlineService;
            _dialogService = dialogService;
            AceptarCommand = new AsyncRelayCommand(AceptarAsync);
            _ = CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            try
            {
                var cuentasTask = _apiClient.ObtenerCuentasAsync();
                var monedasTask = _apiClient.ObtenerMonedasAsync();
                await Task.WhenAll(cuentasTask, monedasTask);

                _todasLasCuentas = cuentasTask.Result;
                MonedasDisponibles = monedasTask.Result.Where(m => m.Codigo != "ARS").OrderBy(m => m.Codigo).ToList();
                if (MonedasDisponibles.Count > 0)
                {
                    MonedaCompra = MonedasDisponibles[0];
                    MonedaVenta = MonedasDisponibles[0];
                }
            }
            catch (Exception ex) { NotificationService.Error("Error al cargar datos", ex.Message); }
        }

        partial void OnMonedaCompraChanged(MonedaDto? value)
        {
            if (value == null) { CuentasCompra = new(); CuentaAcreditaCompra = null; return; }
            CuentasCompra = CuentaAutoComplete.ConstruirTags(_todasLasCuentas, value.Codigo);
            CuentaAcreditaCompra = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, value.Codigo, CuentasCompra);
            _ = CargarCotizacionCompraAsync(value.Codigo);
        }

        partial void OnMonedaVentaChanged(MonedaDto? value)
        {
            if (value == null) { CuentasVenta = new(); CuentaDebitaVenta = null; return; }
            CuentasVenta = CuentaAutoComplete.ConstruirTags(_todasLasCuentas, value.Codigo);
            CuentaDebitaVenta = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, value.Codigo, CuentasVenta);
            _ = CargarCotizacionVentaAsync(value.Codigo);
        }

        private async Task CargarCotizacionCompraAsync(string moneda)
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                var cot = cotizaciones.FirstOrDefault(c => c.CodigoMoneda == moneda);
                CotizacionCompraTexto = (cot?.CotizacionCompra ?? 0m).ToString("N5");
            }
            catch (Exception ex) { AppLogger.Warn("CargarCotizacionCompraAsync", ex); }
        }

        private async Task CargarCotizacionVentaAsync(string moneda)
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                var cot = cotizaciones.FirstOrDefault(c => c.CodigoMoneda == moneda);
                CotizacionVentaTexto = (cot?.CotizacionVenta ?? 0m).ToString("N5");
            }
            catch (Exception ex) { AppLogger.Warn("CargarCotizacionVentaAsync", ex); }
        }

        // ── Cálculo reactivo sin loops: un flag por sección evita que la asignación
        // automática de Pesos dispare un recálculo hacia MontoExtranjero/Cotización.

        partial void OnMontoExtranjeroCompraTextoChanged(string value) => RecalcularCompra();
        partial void OnCotizacionCompraTextoChanged(string value) => RecalcularCompra();

        partial void OnMontoExtranjeroVentaTextoChanged(string value) => RecalcularVenta();
        partial void OnCotizacionVentaTextoChanged(string value) => RecalcularVenta();

        private void RecalcularCompra()
        {
            if (_recalculandoCompra) return;
            decimal monto = MontoHelper.Parsear(MontoExtranjeroCompraTexto);
            decimal cotizacion = MontoHelper.Parsear(CotizacionCompraTexto);
            decimal pesos = Math.Round(monto * cotizacion, 2, MidpointRounding.AwayFromZero);
            _recalculandoCompra = true;
            PesosCompraTexto = pesos.ToString("N2");
            _recalculandoCompra = false;
        }

        private void RecalcularVenta()
        {
            if (_recalculandoVenta) return;
            decimal monto = MontoHelper.Parsear(MontoExtranjeroVentaTexto);
            decimal cotizacion = MontoHelper.Parsear(CotizacionVentaTexto);
            decimal pesos = Math.Round(monto * cotizacion, 2, MidpointRounding.AwayFromZero);
            _recalculandoVenta = true;
            PesosVentaTexto = pesos.ToString("N2");
            _recalculandoVenta = false;
        }

        private async Task AceptarAsync()
        {
            MostrarError = false;
            MensajeError = "";

            if (MonedaCompra == null || CuentaAcreditaCompra == null || MonedaVenta == null || CuentaDebitaVenta == null)
            {
                NotificationService.Warning("Selección incompleta", "Complete todos los campos requeridos.");
                return;
            }

            decimal pesosCompra = MontoHelper.Parsear(PesosCompraTexto);
            decimal pesosVenta = MontoHelper.Parsear(PesosVentaTexto);
            if (pesosCompra <= 0 || pesosVenta != pesosCompra)
            {
                NotificationService.Warning("Montos no coinciden",
                    "El monto en Pesos de la Compra debe ser igual al de la Venta para poder aceptar la operación.");
                return;
            }

            var cuentaPesos = CuentaAutoComplete.PrimeraCajaEfectivo(_todasLasCuentas, "ARS",
                CuentaAutoComplete.ConstruirTags(_todasLasCuentas, "ARS"));
            if (cuentaPesos == null)
            {
                MensajeError = "No se encontró una caja de efectivo en ARS.";
                MostrarError = true;
                return;
            }

            // Confirmación explícita: evita que un Enter accidental dispare la operación sin revisar
            var confirmar = await _dialogService.ConfirmarAsync(
                "Confirmar Compra/Venta",
                $"Comprar {MontoExtranjeroCompraTexto} {MonedaCompra.Codigo} a {CotizacionCompraTexto}\n" +
                $"Acredita en: {CuentaAcreditaCompra.NombreCuenta}\n\n" +
                $"Vender {MontoExtranjeroVentaTexto} {MonedaVenta.Codigo} a {CotizacionVentaTexto}\n" +
                $"Debita de: {CuentaDebitaVenta.NombreCuenta}\n\n¿Confirmar la operación?",
                "Confirmar");
            if (!confirmar) return;

            var request = new CrearArbitrajeRequest
            {
                MonedaCompra = MonedaCompra.Codigo,
                CuentaAcreditaCompraId = CuentaAcreditaCompra.CuentaId,
                MontoExtranjeroCompra = MontoHelper.Parsear(MontoExtranjeroCompraTexto),
                CotizacionCompra = MontoHelper.Parsear(CotizacionCompraTexto),
                PesosCompra = MontoHelper.Parsear(PesosCompraTexto),

                MonedaVenta = MonedaVenta.Codigo,
                CuentaDebitaVentaId = CuentaDebitaVenta.CuentaId,
                MontoExtranjeroVenta = MontoHelper.Parsear(MontoExtranjeroVentaTexto),
                CotizacionVenta = MontoHelper.Parsear(CotizacionVentaTexto),
                PesosVenta = MontoHelper.Parsear(PesosVentaTexto),

                CuentaPesosId = cuentaPesos.CuentaId,
                TipoOperacion = TipoOperacion,
                Observaciones = Observaciones
            };

            try
            {
                var resultado = await _offlineService.GuardarArbitrajeAsync(request);
                if (!resultado.Exitoso)
                {
                    MensajeError = resultado.Mensaje;
                    MostrarError = true;
                    return;
                }
                OperacionGuardada?.Invoke(resultado.OperacionIdCompra ?? 0, resultado.OperacionIdVenta ?? 0, resultado.IsOffline, resultado.Mensaje);
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
