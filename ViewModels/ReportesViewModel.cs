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
    public partial class ReportesViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;

        [ObservableProperty] private DateTimeOffset? _fechaDesdeOp = new DateTimeOffset(DateTime.Today.AddDays(-30));
        [ObservableProperty] private DateTimeOffset? _fechaHastaOp = new DateTimeOffset(DateTime.Today);

        [ObservableProperty] private string _tipoSaldoSeleccionado = "Todos";

        public List<string> TiposSaldoDisponibles { get; } = new() { "Todos", "Caja", "Banco", "Cliente" };

        public ObservableCollection<OperacionDto> Operaciones { get; } = new();
        public ObservableCollection<SaldoReporteRow> Saldos { get; } = new();

        public ICommand GenerarOperacionesCommand { get; }
        public ICommand GenerarSaldosCommand { get; }

        public ReportesViewModel(ICasaCambioApiClient apiClient)
        {
            _apiClient = apiClient;

            GenerarOperacionesCommand = new AsyncRelayCommand(GenerarOperacionesAsync);
            GenerarSaldosCommand = new AsyncRelayCommand(GenerarSaldosAsync);
        }

        private async Task GenerarOperacionesAsync()
        {
            try
            {
                // El servidor compara contra una columna timestamptz: las fechas DEBEN
                // viajar como UTC, o Npgsql rechaza el Kind=Unspecified y no llega nada.
                var fechaDesde = DateTime.SpecifyKind(
                    FechaDesdeOp?.Date ?? DateTime.Today.AddDays(-30), DateTimeKind.Utc);
                var fechaHasta = DateTime.SpecifyKind(
                    (FechaHastaOp?.Date ?? DateTime.Today).AddDays(1), DateTimeKind.Utc);
                var response = await _apiClient.ObtenerOperacionesAsync(fechaDesde, fechaHasta, pageSize: 500);
                Operaciones.Clear();
                foreach (var op in response.Items)
                    Operaciones.Add(op);
            }
            catch (Exception ex) { AppLogger.Warn("GenerarOperacionesAsync", ex); }
        }

        private async Task GenerarSaldosAsync()
        {
            try
            {
                string tipo = TipoSaldoSeleccionado ?? "Todos";
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                cuentas = cuentas.Where(c => c.Tipo != "Externo").ToList();
                if (tipo != "Todos") cuentas = cuentas.Where(c => c.Tipo == tipo).ToList();

                // Aplanar: una fila por (cuenta, saldo). CuentaDto no expone Moneda/Saldo
                // directamente — viven en la lista Saldos (multi-moneda por cuenta).
                var filas = new List<SaldoReporteRow>();
                foreach (var c in cuentas.OrderBy(c => c.Nombre))
                {
                    if (c.Saldos.Count > 0)
                    {
                        foreach (var s in c.Saldos.OrderBy(s => s.Moneda))
                            filas.Add(new SaldoReporteRow
                            {
                                Id = c.Id, Nombre = c.Nombre, Tipo = c.Tipo,
                                Moneda = s.Moneda, Saldo = s.Saldo
                            });
                    }
                    else
                    {
                        // Cuenta sin saldos: una fila placeholder para que siga visible
                        filas.Add(new SaldoReporteRow
                        {
                            Id = c.Id, Nombre = c.Nombre, Tipo = c.Tipo,
                            Moneda = "—", Saldo = 0
                        });
                    }
                }
                Saldos.Clear();
                foreach (var f in filas)
                    Saldos.Add(f);
            }
            catch (Exception ex) { AppLogger.Warn("GenerarSaldosAsync", ex); }
        }

        // ── Exportación CSV ──────────────────────────────────────────
        // StorageProvider/TopLevel viven en la Window, así que el VM solo arma
        // el contenido del CSV como string; el code-behind se ocupa del archivo.

        public string? GenerarCsvOperaciones()
        {
            if (Operaciones == null || !Operaciones.Any()) return null;
            var sb = new StringBuilder();
            sb.AppendLine("ID,Fecha,Tipo,MontoOrigen,MontoDestino,Cotizacion,Observaciones");
            foreach (var op in Operaciones)
                sb.AppendLine($"{op.Id},{op.Fecha:yyyy-MM-dd HH:mm},{op.TipoOperacion},{op.MontoTotalOrigen},{op.MontoTotalDestino},{op.CotizacionAplicada},\"{op.Observaciones}\"");
            return sb.ToString();
        }

        public string? GenerarCsvSaldos()
        {
            if (Saldos == null || !Saldos.Any()) return null;
            var sb = new StringBuilder();
            sb.AppendLine("ID,Cuenta,Tipo,Moneda,Saldo");
            foreach (var f in Saldos)
                sb.AppendLine($"{f.Id},\"{f.Nombre}\",{f.Tipo},{f.Moneda},{f.Saldo}");
            return sb.ToString();
        }

        // ── Exportación PDF ────────────────────────────────────────────

        public byte[]? GenerarPdfOperaciones()
        {
            if (Operaciones == null || !Operaciones.Any()) return null;
            var operaciones = Operaciones.ToList();
            return ConstruirPdf("Reporte de Operaciones", table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(3);
                });
                table.Header(h =>
                {
                    h.Cell().Text("Código").Bold();
                    h.Cell().Text("Fecha").Bold();
                    h.Cell().Text("Tipo").Bold();
                    h.Cell().Text("Monto Origen").Bold();
                    h.Cell().Text("Monto Destino").Bold();
                    h.Cell().Text("Observaciones").Bold();
                });
                foreach (var op in operaciones)
                {
                    table.Cell().Text(op.CodigoOperacion);
                    table.Cell().Text(op.Fecha.ToString("dd/MM/yyyy HH:mm"));
                    table.Cell().Text(op.TipoOperacion);
                    table.Cell().Text(op.MontoTotalOrigen.ToString("N2"));
                    table.Cell().Text(op.MontoTotalDestino.ToString("N2"));
                    table.Cell().Text(op.Observaciones);
                }
            });
        }

        public byte[]? GenerarPdfSaldos()
        {
            if (Saldos == null || !Saldos.Any()) return null;
            var saldos = Saldos.ToList();
            return ConstruirPdf("Reporte de Saldos", table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1);
                    c.RelativeColumn(2);
                });
                table.Header(h =>
                {
                    h.Cell().Text("Cuenta").Bold();
                    h.Cell().Text("Tipo").Bold();
                    h.Cell().Text("Moneda").Bold();
                    h.Cell().Text("Saldo").Bold();
                });
                foreach (var f in saldos)
                {
                    table.Cell().Text(f.Nombre);
                    table.Cell().Text(f.Tipo);
                    table.Cell().Text(f.Moneda);
                    table.Cell().Text(f.Saldo.ToString("N2"));
                }
            });
        }

        private static byte[] ConstruirPdf(string titulo, Action<QuestPDF.Fluent.TableDescriptor> construirTabla)
        {
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
                        col.Item().Text(titulo).FontSize(18).Bold();
                        col.Item().PaddingTop(2).Text($"Generado el {generadoEl:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(15).Table(construirTabla);

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
