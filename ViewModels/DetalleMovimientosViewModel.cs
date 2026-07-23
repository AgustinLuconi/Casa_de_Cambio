using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CasaCambio.Shared.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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

        [ObservableProperty] private string _textoResultados = "0 movimientos encontrados";

        [ObservableProperty] private DateTimeOffset? _fechaDesdeOp = new DateTimeOffset(DateTime.Today.AddDays(-30));
        [ObservableProperty] private DateTimeOffset? _fechaHastaOp = new DateTimeOffset(DateTime.Today);

        [ObservableProperty] private List<FiltroItem<string>> _tiposOperacionFiltro = new();
        [ObservableProperty] private FiltroItem<string>? _tipoOperacionSeleccionado;
        [ObservableProperty] private List<FiltroItem<int>> _cuentasOpFiltro = new();
        [ObservableProperty] private FiltroItem<int>? _cuentaOpSeleccionada;
        [ObservableProperty] private List<FiltroItem<string>> _estadosFiltro = new();
        [ObservableProperty] private FiltroItem<string>? _estadoSeleccionado;
        [ObservableProperty] private string _codigoOperacionBusqueda = "";

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

                var tiposFiltro = new List<FiltroItem<string>>
                {
                    new() { Nombre = "Todas", Valor = "" },
                    new() { Nombre = "Compra", Valor = "Compra" },
                    new() { Nombre = "Venta", Valor = "Venta" },
                    new() { Nombre = "Crédito/Débito", Valor = "Credito/Debito" },
                    new() { Nombre = "Anulación", Valor = "Anulacion" }
                };
                TiposOperacionFiltro = tiposFiltro;
                TipoOperacionSeleccionado = TiposOperacionFiltro[0];

                CuentasOpFiltro = cuentasFiltro;
                CuentaOpSeleccionada = CuentasOpFiltro[0];

                var estadosFiltro = new List<FiltroItem<string>>
                {
                    new() { Nombre = "Todas", Valor = "" },
                    new() { Nombre = "Activas", Valor = "Activa" },
                    new() { Nombre = "Anuladas", Valor = "Anulada" }
                };
                EstadosFiltro = estadosFiltro;
                EstadoSeleccionado = EstadosFiltro[0];
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
                string? tipo = string.IsNullOrEmpty(TipoOperacionSeleccionado?.Valor) ? null : TipoOperacionSeleccionado.Valor;
                var response = await _apiClient.ObtenerOperacionesAsync(fechaDesde, fechaHasta, tipo, pageSize: 500);

                IEnumerable<OperacionDto> resultado = response.Items;

                if (CuentaOpSeleccionada != null && CuentaOpSeleccionada.Valor > 0)
                {
                    var nombreCuenta = CuentaOpSeleccionada.Nombre;
                    resultado = resultado.Where(op => op.Movimientos.Any(m => m.NombreCuenta == nombreCuenta));
                }

                if (EstadoSeleccionado != null && !string.IsNullOrEmpty(EstadoSeleccionado.Valor))
                {
                    bool buscarAnuladas = EstadoSeleccionado.Valor == "Anulada";
                    resultado = resultado.Where(op => op.Anulada == buscarAnuladas);
                }

                if (!string.IsNullOrWhiteSpace(CodigoOperacionBusqueda))
                {
                    var texto = CodigoOperacionBusqueda.Trim();
                    resultado = resultado.Where(op => op.CodigoOperacion.Contains(texto, StringComparison.OrdinalIgnoreCase));
                }

                Operaciones.Clear();
                foreach (var op in resultado)
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

        // ── Exportación (PDF/CSV) — pestaña Movimientos ───────────────
        // Exporta exactamente lo que está cargado en Movimientos (ya filtrado por
        // Buscar), igual que hace la grilla en pantalla.

        public string? GenerarCsvMovimientos()
        {
            if (Movimientos.Count == 0) return null;
            var sb = new StringBuilder();
            sb.AppendLine("Fecha,Codigo,Operacion,Cuenta,Moneda,Debito,Credito,Observaciones");
            foreach (var m in Movimientos)
                sb.AppendLine($"{m.Fecha:yyyy-MM-dd HH:mm},{m.CodigoOperacion},{m.TipoOperacion},\"{m.CuentaNombre}\",{m.Moneda},{m.Debito:0.00},{m.Credito:0.00},\"{m.Observaciones}\"");
            return sb.ToString();
        }

        public byte[]? GenerarPdfMovimientos()
        {
            if (Movimientos.Count == 0) return null;
            var movimientos = Movimientos.ToList();
            var generadoEl = DateTime.Now;

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4.Landscape());
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Movimientos").FontSize(18).Bold();
                        col.Item().PaddingTop(2).Text($"Generado el {generadoEl:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(15).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(3);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Fecha").Bold();
                            h.Cell().Text("Código").Bold();
                            h.Cell().Text("Operación").Bold();
                            h.Cell().Text("Cuenta").Bold();
                            h.Cell().Text("Moneda").Bold();
                            h.Cell().Text("Débito").Bold();
                            h.Cell().Text("Crédito").Bold();
                            h.Cell().Text("Observaciones").Bold();
                        });
                        foreach (var m in movimientos)
                        {
                            table.Cell().Text(m.Fecha.ToString("dd/MM/yyyy HH:mm"));
                            table.Cell().Text(m.CodigoOperacion);
                            table.Cell().Text(m.TipoOperacion);
                            table.Cell().Text(m.CuentaNombre);
                            table.Cell().Text(m.Moneda);
                            table.Cell().Text(m.Debito.ToString("N2"));
                            table.Cell().Text(m.Credito.ToString("N2"));
                            table.Cell().Text(m.Observaciones);
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            });

            return documento.GeneratePdf();
        }
    }
}
