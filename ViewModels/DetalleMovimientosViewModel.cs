using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CasaCambio.Shared.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.ViewModels.Models;

namespace SistemaCambio.ViewModels
{
    public partial class DetalleMovimientosViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IDialogService _dialogService;

        private List<CuentaDto> _cuentasCache = new();

        [ObservableProperty] private List<FiltroItem<int>> _cuentasFiltro = new();
        [ObservableProperty] private List<FiltroItem<int>> _cuentasExternaFiltro = new();
        [ObservableProperty] private List<FiltroItem<string>> _monedasFiltro = new();

        [ObservableProperty] private FiltroItem<int>? _cuentaSeleccionada;
        [ObservableProperty] private FiltroItem<int>? _cuentaExternaSeleccionada;
        [ObservableProperty] private FiltroItem<string>? _monedaSeleccionada;

        [ObservableProperty] private bool _historicosActivado;
        [ObservableProperty] private DateTimeOffset? _fechaDesde = new DateTimeOffset(DateTime.Today);
        [ObservableProperty] private DateTimeOffset? _fechaHasta = new DateTimeOffset(DateTime.Today);

        [ObservableProperty] private string _textoResultados = "0 movimiento(s) encontrado(s)";

        [ObservableProperty] private DateTimeOffset? _fechaDesdeOp = new DateTimeOffset(DateTime.Today.AddDays(-30));
        [ObservableProperty] private DateTimeOffset? _fechaHastaOp = new DateTimeOffset(DateTime.Today);

        public ObservableCollection<MovimientoDetalle> Movimientos { get; } = new();
        public ObservableCollection<OperacionDto> Operaciones { get; } = new();

        public ICommand BuscarCommand { get; }
        public ICommand GenerarCommand { get; }

        public DetalleMovimientosViewModel(ICasaCambioApiClient apiClient, IDialogService dialogService)
        {
            _apiClient = apiClient;
            _dialogService = dialogService;

            BuscarCommand = new AsyncRelayCommand(BuscarAsync);
            GenerarCommand = new AsyncRelayCommand(CargarOperacionesAsync);

            _ = CargarDatosAsync();
            _ = CargarOperacionesAsync();
        }

        // ── Carga inicial ────────────────────────────────────────────

        private async Task CargarDatosAsync()
        {
            try
            {
                _cuentasCache = await _apiClient.ObtenerCuentasAsync();

                var cuentasFiltro = new List<FiltroItem<int>> { new() { Nombre = "Todas", Valor = 0 } };
                var cuentasExternaFiltro = new List<FiltroItem<int>> { new() { Nombre = "Todas", Valor = 0 } };
                foreach (var cuenta in _cuentasCache.Where(c => c.Tipo != "Externo").OrderBy(c => c.Nombre))
                {
                    cuentasFiltro.Add(new FiltroItem<int> { Nombre = cuenta.Nombre, Valor = cuenta.Id });
                    cuentasExternaFiltro.Add(new FiltroItem<int> { Nombre = cuenta.Nombre, Valor = cuenta.Id });
                }
                CuentasFiltro = cuentasFiltro;
                CuentasExternaFiltro = cuentasExternaFiltro;
                CuentaSeleccionada = CuentasFiltro[0];
                CuentaExternaSeleccionada = CuentasExternaFiltro[0];

                var monedasFiltro = new List<FiltroItem<string>> { new() { Nombre = "Todas", Valor = "" } };
                var monedas = _cuentasCache
                    .SelectMany(c => c.Saldos)
                    .Select(s => s.Moneda)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToList();
                foreach (var moneda in monedas)
                    monedasFiltro.Add(new FiltroItem<string> { Nombre = moneda, Valor = moneda });
                MonedasFiltro = monedasFiltro;
                MonedaSeleccionada = MonedasFiltro[0];
            }
            catch (Exception ex) { AppLogger.Warn("CargarDatosAsync", ex); }
        }

        // ── Tab Movimientos ────────────────────────────────────────────────

        private async Task BuscarAsync()
        {
            Movimientos.Clear();

            int cuentaId = CuentaSeleccionada?.Valor ?? 0;

            bool usarRango = HistoricosActivado;
            DateTime fechaDesde = DateTime.SpecifyKind(
                usarRango ? FechaDesde?.DateTime ?? DateTime.Today : DateTime.Today,
                DateTimeKind.Utc);
            DateTime fechaHasta = DateTime.SpecifyKind(
                (usarRango ? FechaHasta?.DateTime ?? DateTime.Today : DateTime.Today).AddDays(1),
                DateTimeKind.Utc);

            var todos = new List<MovimientoDetalle>();
            try
            {
                if (cuentaId > 0)
                {
                    var pagina = await _apiClient.ObtenerMovimientosCuentaAsync(cuentaId, fechaDesde, fechaHasta);
                    foreach (var mov in pagina.Items)
                        todos.Add(new MovimientoDetalle
                        {
                            Id              = mov.Id,
                            CodigoOperacion = $"OP-{mov.OperacionId:D5}",
                            Fecha           = mov.Fecha,
                            TipoOperacion   = "",
                            CuentaNombre    = mov.NombreCuenta,
                            Moneda          = mov.Moneda,
                            Debito          = mov.Monto < 0 ? Math.Abs(mov.Monto) : 0,
                            Credito         = mov.Monto > 0 ? mov.Monto : 0,
                            Observaciones   = ""
                        });
                }
                else
                {
                    var ops = await _apiClient.ObtenerOperacionesAsync(fechaDesde, fechaHasta, pageSize: 200);
                    foreach (var op in ops.Items)
                        foreach (var mov in op.Movimientos)
                            todos.Add(new MovimientoDetalle
                            {
                                Id              = mov.Id,
                                CodigoOperacion = op.CodigoOperacion,
                                Fecha           = mov.Fecha,
                                TipoOperacion   = op.TipoOperacion,
                                CuentaNombre    = mov.NombreCuenta,
                                Moneda          = mov.Moneda,
                                Debito          = mov.Monto < 0 ? Math.Abs(mov.Monto) : 0,
                                Credito         = mov.Monto > 0 ? mov.Monto : 0,
                                Observaciones   = op.Observaciones ?? ""
                            });
                }
            }
            catch (Exception ex) { AppLogger.Warn("BuscarAsync", ex); }

            IEnumerable<MovimientoDetalle> resultado = todos;

            if (MonedaSeleccionada != null && !string.IsNullOrEmpty(MonedaSeleccionada.Valor))
                resultado = resultado.Where(m => m.Moneda == MonedaSeleccionada.Valor);

            if (CuentaExternaSeleccionada != null && CuentaExternaSeleccionada.Valor > 0)
            {
                var nombreExt = CuentaExternaSeleccionada.Nombre;
                if (!string.IsNullOrEmpty(nombreExt) && nombreExt != "Todas")
                    resultado = resultado.Where(m => m.CuentaNombre == nombreExt);
            }

            foreach (var m in resultado)
                Movimientos.Add(m);

            TextoResultados = $"{Movimientos.Count} movimiento(s) encontrado(s)";
        }

        // ── Tab Operaciones ────────────────────────────────────────────────

        private async Task CargarOperacionesAsync()
        {
            try
            {
                var fechaDesde = DateTime.SpecifyKind(
                    FechaDesdeOp?.Date ?? DateTime.Today.AddDays(-30), DateTimeKind.Utc);
                var fechaHasta = DateTime.SpecifyKind(
                    (FechaHastaOp?.Date ?? DateTime.Today).AddDays(1), DateTimeKind.Utc);
                var response = await _apiClient.ObtenerOperacionesAsync(fechaDesde, fechaHasta, pageSize: 500);
                Operaciones.Clear();
                foreach (var op in response.Items)
                    Operaciones.Add(op);
            }
            catch (Exception ex) { AppLogger.Warn("CargarOperacionesAsync", ex); }
        }

        public async Task AnularAsync(OperacionDto op)
        {
            var confirmar = await _dialogService.ConfirmarAsync(
                "Confirmar anulación",
                $"¿Anular la operación {op.CodigoOperacion}?\n\nSe generará una contrapartida que revierte todos los movimientos. Esta acción no se puede deshacer.",
                "Anular operación");
            if (!confirmar) return;

            try
            {
                var resultado = await _apiClient.AnularOperacionAsync(op.Id);
                if (resultado.Exitoso)
                {
                    NotificationService.Success("Anulación registrada",
                        $"{op.CodigoOperacion} anulada. Se generó la contrapartida OP-{resultado.OperacionId:D5}.");
                    await CargarOperacionesAsync();
                }
                else
                    NotificationService.Error("Error al anular", resultado.Mensaje ?? "Error desconocido.");
            }
            catch (Exception ex) { NotificationService.Error("Error", ex.Message); }
        }
    }
}
