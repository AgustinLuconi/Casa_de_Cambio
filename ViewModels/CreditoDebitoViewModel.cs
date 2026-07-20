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
    public partial class CreditoDebitoViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IOfflineOperacionService _offlineService;
        private readonly IDialogService _dialogService;

        private List<CuentaDto> _todasLasCuentas = new();

        [ObservableProperty] private List<MonedaDto> _monedasDisponibles = new();
        [ObservableProperty] private MonedaDto? _monedaSeleccionada;
        [ObservableProperty] private string _monedaNombre = "";

        // Misma lista de cuentas se usa para crédito y débito (ver RefrescarCombosCuentas)
        [ObservableProperty] private List<CuentaMonedaTag> _cuentasDisponibles = new();
        [ObservableProperty] private CuentaMonedaTag? _cuentaCredito;
        [ObservableProperty] private CuentaMonedaTag? _cuentaDebito;

        [ObservableProperty] private string _importeCreditoTexto = "0.00";
        [ObservableProperty] private string _cotizacionCreditoTexto = "0.00000";
        [ObservableProperty] private string _pesosCreditoTexto = "0.00";

        [ObservableProperty] private string _importeDebitoTexto = "0.00";
        [ObservableProperty] private string _cotizacionDebitoTexto = "0.00000";
        [ObservableProperty] private string _pesosDebitoTexto = "0.00";

        [ObservableProperty] private string? _observaciones;

        [ObservableProperty] private bool _mostrarError;
        [ObservableProperty] private string _mensajeError = "";

        public ICommand AceptarCommand { get; }

        public event Action<int, bool, string>? OperacionGuardada;
        public event Action? SolicitarCierre;

        public CreditoDebitoViewModel(ICasaCambioApiClient apiClient, IOfflineOperacionService offlineService, IDialogService dialogService)
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
                MonedasDisponibles = monedasTask.Result.OrderBy(m => m.Codigo).ToList();

                if (MonedasDisponibles.Count > 0)
                    MonedaSeleccionada = MonedasDisponibles[0];
                else
                    RefrescarCombosCuentas(null);
            }
            catch (Exception ex) { NotificationService.Error("Error al cargar datos", ex.Message); }
        }

        partial void OnMonedaSeleccionadaChanged(MonedaDto? value)
        {
            MonedaNombre = value?.Nombre ?? "";
            RefrescarCombosCuentas(value?.Codigo);
            if (value != null)
                _ = CargarCotizacionesDelDiaAsync(value.Codigo);
        }

        private void RefrescarCombosCuentas(string? moneda)
        {
            if (moneda == null)
            {
                CuentasDisponibles = new();
                CuentaCredito = null;
                CuentaDebito = null;
                return;
            }

            var tags = _todasLasCuentas
                .Where(c => c.Tipo != "Externo")
                .Where(c => CuentaFilter.PuedeOperarEnMoneda(c, moneda))
                .OrderBy(c => c.Nombre)
                .Select(c => new CuentaMonedaTag { CuentaId = c.Id, Moneda = moneda, NombreCuenta = c.Nombre })
                .ToList();

            CuentasDisponibles = tags;
            CuentaCredito = tags.FirstOrDefault();
            CuentaDebito = tags.FirstOrDefault();
        }

        private async Task CargarCotizacionesDelDiaAsync(string moneda)
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                var cot = cotizaciones.FirstOrDefault(c => c.CodigoMoneda == moneda);
                if (cot != null)
                {
                    CotizacionCreditoTexto = cot.CotizacionCompra.ToString("N5");
                    CotizacionDebitoTexto = cot.CotizacionVenta.ToString("N5");
                }
                else
                {
                    CotizacionCreditoTexto = "0.00000";
                    CotizacionDebitoTexto = "0.00000";
                }
                RecalcularCredito();
                RecalcularDebito();
            }
            catch (Exception ex) { AppLogger.Warn("CargarCotizacionesDelDiaAsync", ex); }
        }

        // ── Cálculo reactivo ─────────────────────────────────────────

        partial void OnImporteCreditoTextoChanged(string value) => RecalcularCredito();
        partial void OnCotizacionCreditoTextoChanged(string value) => RecalcularCredito();
        partial void OnImporteDebitoTextoChanged(string value) => RecalcularDebito();
        partial void OnCotizacionDebitoTextoChanged(string value) => RecalcularDebito();

        private void RecalcularCredito()
        {
            try
            {
                decimal importe = MontoHelper.Parsear(ImporteCreditoTexto);
                decimal cotizacion = MontoHelper.Parsear(CotizacionCreditoTexto);
                decimal pesos = Math.Round(importe * cotizacion, 2, MidpointRounding.AwayFromZero);
                PesosCreditoTexto = pesos.ToString("N2");
            }
            catch (OverflowException)
            {
                PesosCreditoTexto = "0,00";
            }
        }

        private void RecalcularDebito()
        {
            try
            {
                decimal importe = MontoHelper.Parsear(ImporteDebitoTexto);
                decimal cotizacion = MontoHelper.Parsear(CotizacionDebitoTexto);
                decimal pesos = Math.Round(importe * cotizacion, 2, MidpointRounding.AwayFromZero);
                PesosDebitoTexto = pesos.ToString("N2");
            }
            catch (OverflowException)
            {
                PesosDebitoTexto = "0,00";
            }
        }

        // ── Validación ───────────────────────────────────────────────

        private bool ValidarCampos(decimal importeCredito, decimal importeDebito)
        {
            // Alcanza con que UNO de los dos importes sea mayor a cero (no son 2 patas independientes obligatorias)
            if (importeCredito <= 0 && importeDebito <= 0)
            {
                NotificationService.Warning("Campo requerido", "Ingrese al menos un importe mayor a cero.");
                return false;
            }
            if (CuentaCredito == null || CuentaDebito == null)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione las cuentas crédito y débito.");
                return false;
            }
            return true;
        }

        private void MostrarErrorServidor(string mensaje)
        {
            MostrarError = true;
            MensajeError = mensaje;
            // A diferencia de Compra/Venta, esta ventana además muestra un toast del error de servidor.
            NotificationService.Error("Operación rechazada", mensaje.Split('\n')[0]);
        }

        // ── Aceptar ──────────────────────────────────────────────────

        private async Task AceptarAsync()
        {
            MostrarError = false;
            MensajeError = "";

            try
            {
                decimal importeCredito = MontoHelper.Parsear(ImporteCreditoTexto);
                decimal importeDebito = MontoHelper.Parsear(ImporteDebitoTexto);

                if (!ValidarCampos(importeCredito, importeDebito)) return;

                var tagCredito = CuentaCredito!;
                var tagDebito = CuentaDebito!;

                // En operaciones de la misma moneda no hay tipo de cambio real (ej. ARS-ARS);
                // el textbox de cotización queda en 0 porque no existe cotización de una moneda contra sí misma.
                decimal cotizacion = tagCredito.Moneda == tagDebito.Moneda
                    ? 1m
                    : MontoHelper.Parsear(CotizacionCreditoTexto);

                // Confirmación explícita: evita que un Enter accidental dispare la operación sin revisar
                var confirmar = await _dialogService.ConfirmarAsync(
                    "Confirmar Crédito/Débito",
                    $"Crédito: {tagCredito.NombreCuenta} +{importeCredito:N2} {tagCredito.Moneda}\n" +
                    $"Débito: {tagDebito.NombreCuenta} -{importeDebito:N2} {tagDebito.Moneda}\n\n¿Confirmar la operación?",
                    "Confirmar");
                if (!confirmar) return;

                var request = new CrearCreditoDebitoRequest
                {
                    CuentaCreditoId = tagCredito.CuentaId,
                    CuentaDebitoId = tagDebito.CuentaId,
                    MonedaCredito = tagCredito.Moneda,
                    MonedaDebito = tagDebito.Moneda,
                    MontoCredito = importeCredito,
                    MontoDebito = importeDebito,
                    Cotizacion = cotizacion,
                    Observaciones = Observaciones ?? ""
                };

                var resultado = await _offlineService.GuardarCreditoDebitoAsync(request);

                if (!resultado.Exitoso)
                {
                    MostrarErrorServidor(resultado.Mensaje);
                    return;
                }

                OperacionGuardada?.Invoke(resultado.OperacionId ?? 0, resultado.IsOffline, resultado.Mensaje);
                SolicitarCierre?.Invoke();
            }
            catch (Exception ex)
            {
                MostrarErrorServidor($"Error inesperado: {ex.Message}");
            }
        }
    }
}
