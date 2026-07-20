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
using SistemaCambio.Services.Offline;
using SistemaCambio.ViewModels.Models;

namespace SistemaCambio.ViewModels
{
    public partial class CierreCajaViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IOfflineOperacionService _offlineService;
        private readonly IDialogService _dialogService;
        private int? _cierreId;

        [ObservableProperty] private string _fechaTexto = "";
        [ObservableProperty] private string? _observaciones;

        [ObservableProperty] private string _cantidadComprasTexto = "0";
        [ObservableProperty] private string _comprasUsdTexto = "$0.00";
        [ObservableProperty] private string _comprasArsTexto = "$0.00";
        [ObservableProperty] private string _cantidadVentasTexto = "0";
        [ObservableProperty] private string _ventasUsdTexto = "$0.00";
        [ObservableProperty] private string _ventasArsTexto = "$0.00";
        [ObservableProperty] private string _totalDiferenciasTexto = "$0.00";
        [ObservableProperty] private bool _sinDiferencias = true;

        [ObservableProperty] private bool _borderEstadoVisible;
        [ObservableProperty] private bool _cerrado;
        [ObservableProperty] private string _estadoTexto = "Cierre generado - Sin cerrar definitivamente";
        [ObservableProperty] private bool _btnGenerarHabilitado = true;
        [ObservableProperty] private bool _btnCerrarDefinitivoHabilitado;
        [ObservableProperty] private bool _observacionesSoloLectura;

        [ObservableProperty] private string _cantidadOperacionesTexto = "(0)";

        public ObservableCollection<SaldoCajaItem> Saldos { get; } = new();
        public ObservableCollection<OperacionDto> OperacionesDia { get; } = new();

        public ICommand GenerarCommand { get; }
        public ICommand CerrarDefinitivoCommand { get; }

        public CierreCajaViewModel(ICasaCambioApiClient apiClient, IOfflineOperacionService offlineService, IDialogService dialogService)
        {
            _apiClient = apiClient;
            _offlineService = offlineService;
            _dialogService = dialogService;

            FechaTexto = DateTime.Today.ToString("dddd, dd 'de' MMMM 'de' yyyy");

            GenerarCommand = new AsyncRelayCommand(GenerarAsync);
            CerrarDefinitivoCommand = new AsyncRelayCommand(CerrarDefinitivoAsync);

            _ = CargarSaldosDinamicosAsync();
            _ = CargarCierreExistenteAsync();
            _ = CargarOperacionesDiaAsync();
        }

        private async Task CargarCierreExistenteAsync()
        {
            try
            {
                var cierre = await _apiClient.ObtenerCierreHoyAsync();
                if (cierre != null) MostrarCierre(cierre);
            }
            catch (Exception ex) { AppLogger.Warn("CargarCierreExistenteAsync", ex); }
        }

        private async Task GenerarAsync()
        {
            try
            {
                var cierre = await _apiClient.GenerarCierreAsync(Observaciones ?? "");
                MostrarCierre(cierre);
                BorderEstadoVisible = true;
                BtnCerrarDefinitivoHabilitado = true;
                NotificationService.Info("Cierre generado", "Revise los datos y cierre definitivamente");
            }
            catch (Exception ex) { NotificationService.Error("Error al generar cierre", ex.Message); }
        }

        private void MostrarCierre(CierreCajaDto cierre)
        {
            _cierreId = cierre.Id;
            CantidadComprasTexto = cierre.CantidadCompras.ToString();
            ComprasUsdTexto = $"${cierre.TotalComprasUSD:N2}";
            ComprasArsTexto = $"${cierre.TotalComprasARS:N2}";
            CantidadVentasTexto = cierre.CantidadVentas.ToString();
            VentasUsdTexto = $"${cierre.TotalVentasUSD:N2}";
            VentasArsTexto = $"${cierre.TotalVentasARS:N2}";
            TotalDiferenciasTexto = $"${cierre.TotalDiferencias:N2}";
            SinDiferencias = cierre.TotalDiferencias == 0;

            _ = CargarSaldosDinamicosAsync();
            _ = CargarOperacionesDiaAsync();
            BorderEstadoVisible = true;

            Cerrado = cierre.Cerrado;
            if (cierre.Cerrado)
            {
                EstadoTexto = "Cierre cerrado definitivamente";
                BtnCerrarDefinitivoHabilitado = false;
                BtnGenerarHabilitado = false;
                ObservacionesSoloLectura = true;
            }
            else
            {
                EstadoTexto = "Cierre generado - Sin cerrar definitivamente";
                BtnCerrarDefinitivoHabilitado = true;
            }
        }

        private async Task CargarSaldosDinamicosAsync()
        {
            try
            {
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                var cajas = cuentas.Where(c => c.Tipo == "Efectivo").ToList();
                var saldosGrouped = cajas.SelectMany(c => c.Saldos)
                    .GroupBy(s => s.Moneda)
                    .Select(g => new { Moneda = g.Key, Saldo = g.Sum(x => x.Saldo) })
                    .ToList();

                var items = new List<SaldoCajaItem>();
                string[] colors = { "#3B82F6", "#16A34A", "#D97706", "#DC2626", "#8b5cf6", "#14b8a6" };
                int brushIndex = 0;
                var codigosEspeciales = new[] { "ARS", "USD", "EUR" };
                var todasMonedas = saldosGrouped.Select(s => s.Moneda).Union(codigosEspeciales).Distinct()
                    .OrderBy(x => { var idx = Array.IndexOf(codigosEspeciales, x); return idx == -1 ? 99 : idx; }).ThenBy(x => x);

                foreach (var moneda in todasMonedas)
                {
                    var saldoObj = saldosGrouped.FirstOrDefault(s => s.Moneda == moneda);
                    decimal saldoMonto = saldoObj?.Saldo ?? 0m;
                    string colorHex = colors[brushIndex % colors.Length];
                    string nombreLargo = moneda switch { "ARS" => "PESOS (ARS)", "USD" => "DOLARES (USD)", "EUR" => "EUROS (EUR)", _ => moneda };
                    items.Add(new SaldoCajaItem { Nombre = nombreLargo, SaldoFormatted = $"${saldoMonto:N2}", ColorHex = colorHex });
                    brushIndex++;
                }

                Saldos.Clear();
                foreach (var item in items) Saldos.Add(item);
            }
            catch (Exception ex) { AppLogger.Warn("CargarSaldosDinamicosAsync", ex); }
        }

        private async Task CargarOperacionesDiaAsync()
        {
            try
            {
                var hoy = DateTime.Today;
                var manana = hoy.AddDays(1);
                var response = await _apiClient.ObtenerOperacionesAsync(desde: hoy, hasta: manana, pageSize: 200);
                OperacionesDia.Clear();
                foreach (var op in response.Items) OperacionesDia.Add(op);
                CantidadOperacionesTexto = $"({response.Items.Count})";
            }
            catch (Exception ex) { AppLogger.Warn("CargarOperacionesDiaAsync", ex); }
        }

        private async Task CerrarDefinitivoAsync()
        {
            if (_cierreId == null) return;

            int pendientes = await _offlineService.ObtenerPendientesCountAsync();
            if (pendientes > 0)
            {
                NotificationService.Warning(
                    "Operaciones pendientes",
                    $"Hay {pendientes} operación(es) sin sincronizar. Esperá a que se sincronicen antes de cerrar.");
                return;
            }

            var confirma = await _dialogService.ConfirmarAsync(
                "Cerrar el dia definitivamente?",
                "Esta accion NO se puede deshacer.\n\nUna vez cerrado:\n- No se pueden agregar mas operaciones a este dia\n- Los datos quedan bloqueados para auditoria",
                "Si, cerrar definitivamente", destructivo: true);
            if (!confirma) return;

            try
            {
                var cierre = await _apiClient.CerrarDefinitivoAsync(_cierreId.Value);
                MostrarCierre(cierre);
                NotificationService.CierreCajaCompletado();
            }
            catch (Exception ex) { NotificationService.Error("Error al cerrar", ex.Message); }
        }
    }
}
