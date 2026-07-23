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

namespace SistemaCambio.ViewModels
{
    public partial class DetalleCuentaViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly int _cuentaId;
        private readonly IDialogService _dialogService;

        // null = sin filtro activo (la grilla muestra los últimos 200 y el export trae
        // todo el historial); no-null = resultado del filtro por fecha, reutilizado tal
        // cual tanto para la grilla como para el export, sin repetir la consulta.
        private List<MovimientoDto>? _movimientosFiltrados;

        [ObservableProperty] private string _nombreCuenta = "Nombre de la Cuenta";
        [ObservableProperty] private string _tipoCuenta = "Tipo";
        [ObservableProperty] private bool _sinMovimientos;
        [ObservableProperty] private DateTimeOffset? _fechaDesde;
        [ObservableProperty] private DateTimeOffset? _fechaHasta;

        public ObservableCollection<DetalleSaldoItem> Saldos { get; } = new();
        public ObservableCollection<MovimientoDisplay> Movimientos { get; } = new();

        public ICommand FiltrarCommand { get; }
        public ICommand LimpiarFiltroCommand { get; }

        public event Action? SolicitarCierre;

        public DetalleCuentaViewModel(ICasaCambioApiClient apiClient, int cuentaId, IDialogService dialogService)
        {
            _apiClient = apiClient;
            _cuentaId = cuentaId;
            _dialogService = dialogService;

            FiltrarCommand = new AsyncRelayCommand(FiltrarAsync);
            LimpiarFiltroCommand = new AsyncRelayCommand(LimpiarFiltroAsync);

            _ = CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            try
            {
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                var cuenta = cuentas.FirstOrDefault(c => c.Id == _cuentaId);

                if (cuenta == null)
                {
                    await _dialogService.MensajeAsync("Cuenta no encontrada", "No se pudo encontrar la cuenta solicitada.");
                    SolicitarCierre?.Invoke();
                    return;
                }

                NombreCuenta = cuenta.Nombre;
                TipoCuenta = cuenta.Tipo;

                string[] colors = { "#3B82F6", "#16A34A", "#D97706", "#DC2626", "#8b5cf6", "#14b8a6" };
                int brushIndex = 0;
                foreach (var saldo in cuenta.Saldos.OrderBy(s => s.Moneda))
                {
                    Saldos.Add(new DetalleSaldoItem
                    {
                        Moneda = saldo.Moneda,
                        SaldoFormatted = $"{saldo.Saldo:N2}",
                        ColorHex = colors[brushIndex % colors.Length]
                    });
                    brushIndex++;
                }

                var movimientosPage = await _apiClient.ObtenerMovimientosCuentaAsync(_cuentaId);
                PoblarMovimientos(movimientosPage.Items);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("CargarDatosAsync", ex);
                await _dialogService.MensajeAsync("Error al cargar detalle de cuenta", ex.Message);
                SolicitarCierre?.Invoke();
            }
        }

        private void PoblarMovimientos(IEnumerable<MovimientoDto> items)
        {
            Movimientos.Clear();
            foreach (var m in items)
            {
                string prefijo = m.Monto > 0 ? "+" : "";
                Movimientos.Add(new MovimientoDisplay
                {
                    Fecha = m.Fecha,
                    CodigoOperacion = $"OP-{m.OperacionId:D5}",
                    Moneda = m.Moneda,
                    MontoFormatted = $"{prefijo}{m.Monto:N2}",
                    Monto = m.Monto
                });
            }
            SinMovimientos = Movimientos.Count == 0;
        }

        // ── Filtro por fecha ───────────────────────────────────────────

        private async Task FiltrarAsync()
        {
            if (FechaHasta.HasValue && FechaDesde.HasValue && FechaHasta.Value.Date < FechaDesde.Value.Date)
            {
                NotificationService.Warning("Rango inválido", "La fecha 'Hasta' no puede ser anterior a 'Desde'.");
                return;
            }
            try
            {
                // El servidor compara contra una columna timestamptz: las fechas DEBEN
                // viajar como UTC, o Npgsql rechaza el Kind=Unspecified y no llega nada.
                DateTime? desde = FechaDesde.HasValue ? DateTime.SpecifyKind(FechaDesde.Value.DateTime, DateTimeKind.Utc) : null;
                DateTime? hasta = FechaHasta.HasValue ? DateTime.SpecifyKind(FechaHasta.Value.DateTime.AddDays(1), DateTimeKind.Utc) : null;
                _movimientosFiltrados = await ObtenerTodosLosMovimientosAsync(desde, hasta);
                PoblarMovimientos(_movimientosFiltrados);
            }
            catch (Exception ex) { NotificationService.Error("Error al filtrar", ex.Message); }
        }

        private async Task LimpiarFiltroAsync()
        {
            FechaDesde = null;
            FechaHasta = null;
            _movimientosFiltrados = null;
            try
            {
                var movimientosPage = await _apiClient.ObtenerMovimientosCuentaAsync(_cuentaId);
                PoblarMovimientos(movimientosPage.Items);
            }
            catch (Exception ex) { NotificationService.Error("Error al limpiar filtro", ex.Message); }
        }

        // ── Exportación (PDF/CSV) ─────────────────────────────────────
        // StorageProvider/TopLevel viven en la Window, así que el VM solo arma
        // el contenido; el code-behind se ocupa del diálogo de guardado. Si hay un
        // filtro de fecha activo, el export reutiliza ese mismo resultado (no vuelve
        // a traer todo el historial); si no, trae todo el historial paginando.

        private async Task<List<MovimientoDto>> ObtenerTodosLosMovimientosAsync(DateTime? desde = null, DateTime? hasta = null)
        {
            var todos = new List<MovimientoDto>();
            int page = 1;
            const int pageSize = 200;
            while (true)
            {
                var pagina = await _apiClient.ObtenerMovimientosCuentaAsync(_cuentaId, desde, hasta, page, pageSize);
                todos.AddRange(pagina.Items);
                if (todos.Count >= pagina.TotalCount || pagina.Items.Count < pageSize) break;
                page++;
            }
            // El servidor devuelve más reciente primero; un estado de cuenta se lee cronológicamente.
            todos.Reverse();
            return todos;
        }

        public async Task<string> GenerarCsvEstadoCuentaAsync()
        {
            var movimientos = _movimientosFiltrados ?? await ObtenerTodosLosMovimientosAsync();
            return GenerarCsvEstadoCuenta(movimientos);
        }

        public async Task<byte[]> GenerarPdfEstadoCuentaAsync()
        {
            var movimientos = _movimientosFiltrados ?? await ObtenerTodosLosMovimientosAsync();
            return GenerarPdfEstadoCuenta(movimientos);
        }

        private string GenerarCsvEstadoCuenta(List<MovimientoDto> movimientos)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Cuenta,{NombreCuenta}");
            sb.AppendLine($"Tipo,{TipoCuenta}");
            sb.AppendLine();
            sb.AppendLine("Saldos Actuales");
            sb.AppendLine("Moneda,Saldo");
            foreach (var s in Saldos)
                sb.AppendLine($"{s.Moneda},{s.SaldoFormatted}");
            sb.AppendLine();
            sb.AppendLine("Movimientos");
            sb.AppendLine("Fecha,Codigo Operacion,Moneda,Monto");
            foreach (var m in movimientos)
                sb.AppendLine($"{m.Fecha:yyyy-MM-dd HH:mm},OP-{m.OperacionId:D5},{m.Moneda},{m.Monto:0.00}");
            return sb.ToString();
        }

        private byte[] GenerarPdfEstadoCuenta(List<MovimientoDto> movimientos)
        {
            var nombreCuenta = NombreCuenta;
            var tipoCuenta = TipoCuenta;
            var saldos = Saldos.ToList();
            var generadoEl = DateTime.Now;

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Estado de Cuenta").FontSize(18).Bold();
                        col.Item().Text($"{nombreCuenta}  ·  {tipoCuenta}").FontSize(12);
                        col.Item().PaddingTop(2).Text($"Generado el {generadoEl:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        col.Item().Text("Saldos Actuales").FontSize(12).Bold();
                        col.Item().PaddingBottom(10).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h =>
                            {
                                h.Cell().Text("Moneda").Bold();
                                h.Cell().Text("Saldo").Bold();
                            });
                            foreach (var s in saldos)
                            {
                                table.Cell().Text(s.Moneda);
                                table.Cell().Text(s.SaldoFormatted);
                            }
                        });

                        col.Item().Text("Movimientos").FontSize(12).Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(1);
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Text("Fecha").Bold();
                                h.Cell().Text("Código Operación").Bold();
                                h.Cell().Text("Moneda").Bold();
                                h.Cell().Text("Monto").Bold();
                            });
                            foreach (var m in movimientos)
                            {
                                table.Cell().Text(m.Fecha.ToString("dd/MM/yyyy HH:mm"));
                                table.Cell().Text($"OP-{m.OperacionId:D5}");
                                table.Cell().Text(m.Moneda);
                                table.Cell().Text(m.Monto.ToString("N2"));
                            }
                        });
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

    public class MovimientoDisplay
    {
        public DateTime Fecha { get; set; }
        public string CodigoOperacion { get; set; } = "";
        public string Moneda { get; set; } = "";
        public string MontoFormatted { get; set; } = "";
        public decimal Monto { get; set; }
    }

    public class DetalleSaldoItem
    {
        public string Moneda { get; set; } = "";
        public string SaldoFormatted { get; set; } = "";
        public string ColorHex { get; set; } = "";
    }
}
