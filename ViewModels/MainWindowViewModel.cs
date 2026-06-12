using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using CasaCambio.Shared.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;

namespace SistemaCambio.ViewModels
{
    public class SaldoInfo
    {
        public string Moneda { get; set; } = "";
        public decimal Saldo { get; set; }
    }

    public class CuentaGrupoItem
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string SaldosResumen { get; set; } = "";
        public int CantidadMonedas { get; set; }
        public List<SaldoInfo> Saldos { get; set; } = new();
        public bool TieneSaldos => Saldos.Count > 0;
        public bool NoTieneSaldos => Saldos.Count == 0;
    }

    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private List<CuentaGrupoItem> _cuentasTodas = new();

        [ObservableProperty]
        private ObservableCollection<CuentaGrupoItem> cuentas;

        [ObservableProperty] private string textoBusqueda = "";
        [ObservableProperty] private string filtroTipoSeleccionado = "Todos los Tipos";
        [ObservableProperty] private string filtroMonedaSeleccionada = "Todas las Monedas";

        public List<string> TiposFiltro { get; } = new() { "Todos los Tipos", "Efectivo", "Banco", "Cliente", "Proveedor" };
        public List<string> MonedasFiltro { get; } = new() { "Todas las Monedas", "USD", "EUR", "ARS" };

        [ObservableProperty]
        private int cuentasCount;

        [ObservableProperty]
        private decimal totalDebito;

        [ObservableProperty]
        private decimal totalCredito;

        [ObservableProperty]
        private decimal posicionNeta;

        [ObservableProperty] private int _totalOperacionesHoy;
        [ObservableProperty] private int _totalComprasHoy;
        [ObservableProperty] private int _totalVentasHoy;
        [ObservableProperty] private decimal _volumenComprasARS;
        [ObservableProperty] private decimal _volumenVentasARS;
        [ObservableProperty] private decimal _volumenNetoARS;
        [ObservableProperty] private List<CotizacionDto> _cotizacionesHoy = new();

        public ICommand LimpiarFiltrosCommand { get; }
        public ICommand AbrirDashboardCommand { get; }
        public ICommand AbrirCuentasCommand { get; }
        public ICommand AbrirCompraCommand { get; }
        public ICommand AbrirVentaCommand { get; }
        public ICommand AbrirCreditoDebitoCommand { get; }
        public ICommand AbrirArqueoCommand { get; }
        public ICommand AbrirMovimientosCommand { get; }
        public ICommand VerDetalleCuentaCommand { get; }
        public ICommand EditarCuentaCommand { get; }
        public ICommand EliminarCuentaCommand { get; }

        public event Action<string>? SolicitarAbrirVentana;
        public event Action<int>? SolicitarDetalleCuenta;
        public event Action<int>? SolicitarEdicionCuenta;
        public event Action<string, string>? MostrarMensajeEvent;
        public event Func<string, string, System.Threading.Tasks.Task<bool>>? MostrarConfirmacionEvent;
        public event Action<DashboardDto>? DashboardCargado;

        public MainWindowViewModel(ICasaCambioApiClient apiClient)
        {
            _apiClient = apiClient;
            Cuentas = new ObservableCollection<CuentaGrupoItem>();

            LimpiarFiltrosCommand = new RelayCommand(() =>
            {
                TextoBusqueda = "";
                FiltroTipoSeleccionado = "Todos los Tipos";
                FiltroMonedaSeleccionada = "Todas las Monedas";
            });

            AbrirDashboardCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Dashboard"));
            AbrirCuentasCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Cuentas"));
            AbrirCompraCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Compra"));
            AbrirVentaCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Venta"));
            AbrirCreditoDebitoCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("CreditoDebito"));
            AbrirArqueoCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Arqueo"));
            AbrirMovimientosCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Movimientos"));

            VerDetalleCuentaCommand = new RelayCommand<CuentaGrupoItem>(obj =>
            {
                if (obj != null) SolicitarDetalleCuenta?.Invoke(obj.Id);
            });

            EditarCuentaCommand = new RelayCommand<CuentaGrupoItem>(obj =>
            {
                if (obj != null) SolicitarEdicionCuenta?.Invoke(obj.Id);
            });

            EliminarCuentaCommand = new AsyncRelayCommand<CuentaGrupoItem>(async (obj) =>
            {
                if (obj == null || MostrarConfirmacionEvent == null) return;

                bool confirma = await MostrarConfirmacionEvent.Invoke(
                    "Eliminar Cuenta",
                    $"¿Está seguro que desea eliminar la cuenta \"{obj.Nombre}\"?\nEsta acción no se puede deshacer.");

                if (!confirma) return;

                try
                {
                    await _apiClient.EliminarCuentaAsync(obj.Id);
                    Cuentas.Remove(obj);
                    MostrarMensajeEvent?.Invoke("Éxito", $"La cuenta \"{obj.Nombre}\" fue eliminada.");
                }
                catch (HttpRequestException ex)
                {
                    MostrarMensajeEvent?.Invoke("Error", ex.Message);
                }
            });

            CargarDatosAsync();
        }

        public void RefrescarDatos()
        {
            _cuentasTodas.Clear();
            Cuentas.Clear();
            TextoBusqueda = "";
            FiltroTipoSeleccionado = "Todos los Tipos";
            FiltroMonedaSeleccionada = "Todas las Monedas";
            CargarDatosAsync();
        }

        partial void OnTextoBusquedaChanged(string value) => AplicarFiltros();
        partial void OnFiltroTipoSeleccionadoChanged(string value) => AplicarFiltros();
        partial void OnFiltroMonedaSeleccionadaChanged(string value) => AplicarFiltros();

        private void AplicarFiltros()
        {
            var query = _cuentasTodas.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                var busqueda = TextoBusqueda.Trim().ToLowerInvariant();
                query = query.Where(c =>
                    c.Id.ToString().Contains(busqueda) ||
                    c.Nombre.ToLowerInvariant().Contains(busqueda));
            }

            if (FiltroTipoSeleccionado != "Todos los Tipos")
                query = query.Where(c => c.Tipo == FiltroTipoSeleccionado);

            if (FiltroMonedaSeleccionada != "Todas las Monedas")
                query = query.Where(c => c.Saldos.Any(s => s.Moneda == FiltroMonedaSeleccionada));

            Cuentas.Clear();
            foreach (var c in query)
                Cuentas.Add(c);
        }

        private async void CargarDatosAsync()
        {
            try
            {
                var cuentasTask   = _apiClient.ObtenerCuentasAsync();
                var dashboardTask = _apiClient.ObtenerDashboardAsync();
                await Task.WhenAll(cuentasTask, dashboardTask);
                var cuentasApi = cuentasTask.Result;

                try
                {
                    var dashboard = dashboardTask.Result;
                    TotalOperacionesHoy = dashboard.TotalOperacionesHoy;
                    TotalComprasHoy     = dashboard.TotalComprasHoy;
                    TotalVentasHoy      = dashboard.TotalVentasHoy;
                    VolumenComprasARS   = dashboard.VolumenComprasARS;
                    VolumenVentasARS    = dashboard.VolumenVentasARS;
                    VolumenNetoARS      = dashboard.VolumenComprasARS + dashboard.VolumenVentasARS;
                    CotizacionesHoy     = dashboard.CotizacionesHoy;
                    DashboardCargado?.Invoke(dashboard);
                }
                catch (Exception exDash)
                {
                    Console.WriteLine($"Error cargando dashboard: {exDash.Message}");
                }

                // Una sola fila por cuenta (agrupada por propietario)
                foreach (var cuenta in cuentasApi.OrderBy(c => c.Id))
                {
                    var saldos = cuenta.Saldos
                        .OrderBy(s => s.Moneda)
                        .Select(s => new SaldoInfo { Moneda = s.Moneda, Saldo = s.Saldo })
                        .ToList();

                    var resumen = saldos.Any()
                        ? string.Join("  |  ", saldos.Select(s => $"{s.Moneda}: {s.Saldo:N2}"))
                        : "Sin saldos";

                    _cuentasTodas.Add(new CuentaGrupoItem
                    {
                        Id = cuenta.Id,
                        Nombre = cuenta.Nombre,
                        Tipo = cuenta.Tipo,
                        SaldosResumen = resumen,
                        CantidadMonedas = saldos.Count,
                        Saldos = saldos
                    });
                }
                AplicarFiltros();

                CuentasCount = _cuentasTodas.Count;
                var allSaldos = cuentasApi.SelectMany(c => c.Saldos).ToList();
                TotalDebito = allSaldos.Where(s => s.Saldo < 0).Sum(s => Math.Abs(s.Saldo));
                TotalCredito = allSaldos.Where(s => s.Saldo > 0).Sum(s => s.Saldo);
                PosicionNeta = TotalCredito - TotalDebito;
            }
            catch (Exception ex)
            {
                Cuentas.Add(new CuentaGrupoItem { Nombre = $"Error: {ex.Message}" });
            }
        }
    }
}
