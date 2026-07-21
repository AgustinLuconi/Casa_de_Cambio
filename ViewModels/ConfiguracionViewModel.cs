using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels.Models;

namespace SistemaCambio.ViewModels
{
    public partial class ConfiguracionViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IDialogService _dialogService;

        // ── Tab Monedas ────────────────────────────────────────────────

        [ObservableProperty] private List<MonedaDto> _monedas = new();

        [ObservableProperty] private string _nuevoCodigo = "";
        [ObservableProperty] private string _nuevoNombre = "";
        [ObservableProperty] private string _nuevoTipoPase = "D";

        public List<string> TiposPaseDisponibles { get; } = new() { "D", "M" };

        public ICommand AgregarMonedaCommand { get; }
        public ICommand GuardarCambiosCommand { get; }
        public ICommand RefrescarCommand { get; }

        // ── Tab Cotizaciones ───────────────────────────────────────────

        [ObservableProperty] private List<string> _codigosMonedaCotiz = new();
        [ObservableProperty] private string? _monedaCotizSeleccionada;
        [ObservableProperty] private string _cotizCompraTexto = "0.00000";

        public ObservableCollection<CotizacionView> Cotizaciones { get; } = new();

        public ICommand CargarCotizacionesCommand { get; }
        public ICommand GuardarCotizacionCommand { get; }

        // ── Tab General — Límites de deuda por divisa ─────────────────
        // Clave de configuración por moneda: limite_deuda_general_{CODIGO}

        public ObservableCollection<LimiteDivisaModel> LimitesDivisa { get; } = new();

        [ObservableProperty] private bool _sinLimiteGeneral;
        [ObservableProperty] private bool _limitesDivisaHabilitado = true;

        public ICommand GuardarLimitesDivisaCommand { get; }

        public ConfiguracionViewModel(ICasaCambioApiClient apiClient, IDialogService dialogService)
        {
            _apiClient = apiClient;
            _dialogService = dialogService;

            AgregarMonedaCommand = new AsyncRelayCommand(AgregarMonedaAsync);
            GuardarCambiosCommand = new AsyncRelayCommand(GuardarCambiosAsync);
            RefrescarCommand = new AsyncRelayCommand(CargarMonedasAsync);

            CargarCotizacionesCommand = new AsyncRelayCommand(CargarCotizacionesAsync);
            GuardarCotizacionCommand = new AsyncRelayCommand(GuardarCotizacionAsync);

            GuardarLimitesDivisaCommand = new AsyncRelayCommand(GuardarLimitesDivisaAsync);

            _ = CargarMonedasAsync();
        }

        // ── Tab Monedas ────────────────────────────────────────────────

        private async Task CargarMonedasAsync()
        {
            try
            {
                var monedas = await _apiClient.ObtenerMonedasAsync();
                Monedas = monedas;
                CodigosMonedaCotiz = monedas.Select(m => m.Codigo).ToList();
                MonedaCotizSeleccionada = CodigosMonedaCotiz.FirstOrDefault();
                await CargarLimitesDivisaAsync(monedas);
            }
            catch (Exception ex) { await _dialogService.MensajeAsync("Error", ex.Message); }
        }

        private async Task AgregarMonedaAsync()
        {
            var codigo = NuevoCodigo?.Trim().ToUpper();
            var nombre = NuevoNombre?.Trim();
            var tipoPase = NuevoTipoPase ?? "D";
            if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(nombre))
            {
                await _dialogService.MensajeAsync("Error", "Debe ingresar el codigo y el nombre de la moneda.");
                return;
            }

            try
            {
                await _apiClient.CrearMonedaAsync(new CrearMonedaRequest { Codigo = codigo, Nombre = nombre, TipoPase = tipoPase });
                NuevoCodigo = "";
                NuevoNombre = "";
                await CargarMonedasAsync();
            }
            catch (Exception ex) { await _dialogService.MensajeAsync("Error", ex.Message); }
        }

        private async Task GuardarCambiosAsync()
        {
            var monedas = Monedas.ToList();
            var errores = new List<string>();
            foreach (var m in monedas)
            {
                try
                {
                    await _apiClient.ActualizarMonedaAsync(m.Id, new ActualizarMonedaRequest { Codigo = m.Codigo, Nombre = m.Nombre, Activa = m.Activa, TipoPase = m.TipoPase });
                }
                catch (HttpRequestException ex) { errores.Add($"{m.Codigo}: {ex.Message}"); }
            }

            if (errores.Any())
                await _dialogService.MensajeAsync("Error", string.Join("\n", errores));
            else
                await _dialogService.MensajeAsync("Éxito", $"{monedas.Count} moneda(s) actualizadas correctamente.");

            await CargarMonedasAsync();
        }

        public async Task EliminarMonedaAsync(MonedaDto moneda)
        {
            var resultado = await _dialogService.ConfirmarAsync(
                "Eliminar Moneda",
                $"¿Está seguro que desea eliminar la moneda \"{moneda.Codigo} - {moneda.Nombre}\"?\nEsta acción no se puede deshacer.",
                "Sí, eliminar", destructivo: true);
            if (!resultado) return;

            try
            {
                await _apiClient.EliminarMonedaAsync(moneda.Id);
                await _dialogService.MensajeAsync("Éxito", $"La moneda \"{moneda.Codigo}\" fue eliminada.");
                await CargarMonedasAsync();
            }
            catch (HttpRequestException ex) { await _dialogService.MensajeAsync("Error", ex.Message); }
        }

        // ── Tab Cotizaciones ───────────────────────────────────────────

        private async Task CargarCotizacionesAsync()
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                Cotizaciones.Clear();
                foreach (var c in cotizaciones)
                    Cotizaciones.Add(new CotizacionView
                    {
                        MonedaCodigo = c.CodigoMoneda,
                        Fecha = c.Fecha,
                        CotizacionCompra = c.CotizacionCompra,
                        CotizacionVenta = c.CotizacionVenta
                    });
            }
            catch (Exception ex) { await _dialogService.MensajeAsync("Error", ex.Message); }
        }

        private async Task GuardarCotizacionAsync()
        {
            var codigoMoneda = MonedaCotizSeleccionada;
            if (string.IsNullOrEmpty(codigoMoneda)) return;

            decimal cotizCompra = MontoHelper.Parsear(CotizCompraTexto);
            if (cotizCompra <= 0) return;

            try
            {
                await _apiClient.GuardarCotizacionAsync(new CrearCotizacionRequest
                {
                    CodigoMoneda = codigoMoneda,
                    CotizacionCompra = cotizCompra,
                    CotizacionVenta = cotizCompra * 1.02m
                });
                await CargarCotizacionesAsync();
            }
            catch (Exception ex) { await _dialogService.MensajeAsync("Error", ex.Message); }
        }

        // ── Tab General — Límites de deuda por divisa ─────────────────

        private async Task CargarLimitesDivisaAsync(List<MonedaDto> monedas)
        {
            try
            {
                // Una consulta por divisa, lanzadas en paralelo
                var tareas = monedas.OrderBy(m => m.Codigo).Select(async m =>
                {
                    var valor = await _apiClient.ObtenerConfiguracionAsync($"limite_deuda_general_{m.Codigo}");
                    return new LimiteDivisaModel
                    {
                        Codigo = m.Codigo,
                        Nombre = m.Nombre,
                        LimiteTexto = valor ?? "0"
                    };
                }).ToList();

                var items = await Task.WhenAll(tareas);

                LimitesDivisa.Clear();
                foreach (var item in items)
                    LimitesDivisa.Add(item);

                // Auto-marcar el toggle si TODAS las divisas tienen límite 0
                var todasEnCero = LimitesDivisa.All(m => MontoHelper.Parsear(m.LimiteTexto) == 0);
                SinLimiteGeneral = todasEnCero;
                LimitesDivisaHabilitado = !todasEnCero;
            }
            catch (Exception ex) { AppLogger.Warn("CargarLimitesDivisaAsync", ex); }
        }

        partial void OnSinLimiteGeneralChanged(bool value)
        {
            LimitesDivisaHabilitado = !value;

            if (value)
            {
                // Mostrar 0 en todos los campos visualmente, sin guardar aún
                foreach (var item in LimitesDivisa)
                    item.LimiteTexto = "0";

                // LimiteDivisaModel no implementa INotifyPropertyChanged: forzar
                // un Reset de la colección para que el ItemsControl repinte.
                var itemsActuales = LimitesDivisa.ToList();
                LimitesDivisa.Clear();
                foreach (var item in itemsActuales) LimitesDivisa.Add(item);
            }
        }

        private async Task GuardarLimitesDivisaAsync()
        {
            var errores = new List<string>();
            int guardados = 0;

            // Si el toggle está activo, forzar 0 en todos
            var sinLimite = SinLimiteGeneral;

            foreach (var item in LimitesDivisa)
            {
                decimal limite = sinLimite ? 0 : MontoHelper.Parsear(item.LimiteTexto);
                if (!sinLimite && limite < 0)
                {
                    errores.Add($"{item.Codigo}: el límite no puede ser negativo.");
                    continue;
                }
                var ok = await _apiClient.ActualizarConfiguracionAsync(
                    $"limite_deuda_general_{item.Codigo}",
                    limite.ToString(CultureInfo.InvariantCulture));
                if (ok) guardados++;
                else errores.Add($"{item.Codigo}: no se pudo guardar.");
            }

            if (errores.Any())
                await _dialogService.MensajeAsync("Error", string.Join("\n", errores));
            else
            {
                var msg = sinLimite
                    ? $"Límites desactivados para todas las divisas ({guardados} divisa(s))."
                    : $"Límites de deuda actualizados para {guardados} divisa(s).";
                await _dialogService.MensajeAsync("Éxito", msg);
            }
        }
    }
}
