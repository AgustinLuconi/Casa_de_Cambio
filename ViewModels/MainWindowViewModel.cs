using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.Models;

namespace SistemaCambio.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<Cuenta> cuentas;

        [ObservableProperty]
        private int cuentasCount;

        [ObservableProperty]
        private decimal totalDebito;

        [ObservableProperty]
        private decimal totalCredito;

        [ObservableProperty]
        private decimal posicionNeta;

        // Commands para atajos de teclado - se manejan en code-behind
        public ICommand AbrirDashboardCommand { get; }
        public ICommand AbrirCompraCommand { get; }
        public ICommand AbrirVentaCommand { get; }
        public ICommand AbrirCreditoDebitoCommand { get; }
        public ICommand AbrirArqueoCommand { get; }
        public ICommand AbrirMovimientosCommand { get; }

        // Evento para notificar a la vista que debe abrir una ventana
        public event Action<string>? SolicitarAbrirVentana;

        public MainWindowViewModel()
        {
            Cuentas = new ObservableCollection<Cuenta>();
            
            // Inicializar commands
            AbrirDashboardCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Dashboard"));
            AbrirCompraCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Compra"));
            AbrirVentaCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Venta"));
            AbrirCreditoDebitoCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("CreditoDebito"));
            AbrirArqueoCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Arqueo"));
            AbrirMovimientosCommand = new RelayCommand(() => SolicitarAbrirVentana?.Invoke("Movimientos"));

            CargarDatosDeBaseDeDatos();
        }

        public void RefrescarDatos()
        {
            Cuentas.Clear();
            CargarDatosDeBaseDeDatos();
        }

        private void CargarDatosDeBaseDeDatos()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    if (db.Database.CanConnect())
                    {
                        var listaCuentas = db.Cuentas.OrderBy(c => c.Id).ToList();

                        foreach (var cuenta in listaCuentas)
                        {
                            Cuentas.Add(cuenta);
                        }

                        // Calcular estadísticas
                        CuentasCount = Cuentas.Count;
                        
                        // Calcular totales (saldos negativos = débito, positivos = crédito)
                        TotalDebito = Cuentas.Where(c => c.Saldo < 0).Sum(c => Math.Abs(c.Saldo));
                        TotalCredito = Cuentas.Where(c => c.Saldo > 0).Sum(c => c.Saldo);
                        PosicionNeta = TotalCredito - TotalDebito;
                    }
                    else
                    {
                        Cuentas.Add(new Cuenta { Nombre = "ERROR: NO CONECTA A DB", Saldo = 0 });
                    }
                }
            }
            catch (Exception ex)
            {
                Cuentas.Add(new Cuenta { Nombre = $"Error: {ex.Message}", Saldo = 0 });
            }
        }
    }
}