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
    }
}
