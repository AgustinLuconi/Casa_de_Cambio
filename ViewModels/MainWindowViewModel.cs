using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;

namespace SistemaCambio.ViewModels
{
    public class CuentaResumenItem
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Moneda { get; set; } = "";
        public decimal Saldo { get; set; }
    }

    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;

        [ObservableProperty]
        private ObservableCollection<CuentaResumenItem> cuentas;

        [ObservableProperty]
        private int cuentasCount;

        [ObservableProperty]
        private decimal totalDebito;

        [ObservableProperty]
        private decimal totalCredito;

        [ObservableProperty]
        private decimal posicionNeta;

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

        public MainWindowViewModel(ICasaCambioApiClient apiClient)
        {
            _apiClient = apiClient;
            Cuentas = new ObservableCollection<CuentaResumenItem>();

            AbrirDashboardCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Dashboard"));
            AbrirCuentasCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Cuentas"));
            AbrirCompraCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Compra"));
            AbrirVentaCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Venta"));
            AbrirCreditoDebitoCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("CreditoDebito"));
            AbrirArqueoCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Arqueo"));
            AbrirMovimientosCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Movimientos"));

            VerDetalleCuentaCommand = new RelayCommand<CuentaResumenItem>(obj =>
            {
                if (obj != null) SolicitarDetalleCuenta?.Invoke(obj.Id);
            });

            EditarCuentaCommand = new RelayCommand<CuentaResumenItem>(obj =>
            {
                if (obj != null) SolicitarEdicionCuenta?.Invoke(obj.Id);
            });

            EliminarCuentaCommand = new AsyncRelayCommand<CuentaResumenItem>(async (obj) =>
            {
                if (obj == null || MostrarConfirmacionEvent == null) return;

                bool confirma = await MostrarConfirmacionEvent.Invoke(
                    "Eliminar Cuenta",
                    $"Esta funcionalidad requiere conexion al servidor.\n\n¿Desea continuar?");

                if (!confirma) return;

                MostrarMensajeEvent?.Invoke("Info", "La eliminacion de cuentas se realiza desde el servidor.");
            });

            CargarDatosAsync();
        }

        public void RefrescarDatos()
        {
            Cuentas.Clear();
            CargarDatosAsync();
        }

        private async void CargarDatosAsync()
        {
            try
            {
                var cuentasApi = await _apiClient.ObtenerCuentasAsync();

                foreach (var cuenta in cuentasApi.OrderBy(c => c.Id))
                {
                    if (cuenta.Saldos.Any())
                    {
                        foreach (var saldo in cuenta.Saldos)
                        {
                            Cuentas.Add(new CuentaResumenItem
                            {
                                Id = cuenta.Id,
                                Nombre = cuenta.Nombre,
                                Tipo = cuenta.Tipo,
                                Moneda = saldo.Moneda,
                                Saldo = saldo.Saldo
                            });
                        }
                    }
                    else
                    {
                        Cuentas.Add(new CuentaResumenItem
                        {
                            Id = cuenta.Id,
                            Nombre = cuenta.Nombre,
                            Tipo = cuenta.Tipo,
                            Moneda = "ARS",
                            Saldo = 0
                        });
                    }
                }

                CuentasCount = cuentasApi.Count;
                var allSaldos = cuentasApi.SelectMany(c => c.Saldos).ToList();
                TotalDebito = allSaldos.Where(s => s.Saldo < 0).Sum(s => Math.Abs(s.Saldo));
                TotalCredito = allSaldos.Where(s => s.Saldo > 0).Sum(s => s.Saldo);
                PosicionNeta = TotalCredito - TotalDebito;
            }
            catch (Exception ex)
            {
                Cuentas.Add(new CuentaResumenItem { Nombre = $"Error: {ex.Message}" });
            }
        }
    }
}
