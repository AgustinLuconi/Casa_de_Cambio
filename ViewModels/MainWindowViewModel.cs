using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;

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
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

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

        public MainWindowViewModel(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
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
                if (obj != null)
                {
                    SolicitarDetalleCuenta?.Invoke(obj.Id);
                }
            });

            EditarCuentaCommand = new RelayCommand<CuentaResumenItem>(obj =>
            {
                if (obj != null)
                {
                    SolicitarEdicionCuenta?.Invoke(obj.Id);
                }
            });

            EliminarCuentaCommand = new AsyncRelayCommand<CuentaResumenItem>(async (obj) =>
            {
                if (obj == null) return;

                if (MostrarConfirmacionEvent == null) return;

                bool confirma = await MostrarConfirmacionEvent.Invoke(
                    "Eliminar Cuenta", 
                    $"¿Está seguro que desea eliminar la cuenta '{obj.Nombre}'?\n\nEsta acción eliminará también sus saldos iniciales y no se puede deshacer.");

                if (!confirma) return;

                try
                {
                    using var db = _contextFactory.CreateDbContext();

                    // Check for operations linked to this account
                    bool tieneMovimientos = await db.Movimientos.AnyAsync(m => m.CuentaId == obj.Id);
                    
                    if (tieneMovimientos)
                    {
                        MostrarMensajeEvent?.Invoke("Error", $"No se puede eliminar la cuenta '{obj.Nombre}' porque tiene movimientos u operaciones asociadas.");
                        return;
                    }

                    var cuenta = await db.Cuentas.Include(c => c.Saldos).FirstOrDefaultAsync(c => c.Id == obj.Id);
                    if (cuenta != null)
                    {
                        db.SaldosCuenta.RemoveRange(cuenta.Saldos);
                        db.Cuentas.Remove(cuenta);
                        await db.SaveChangesAsync();
                        
                        RefrescarDatos();
                        MostrarMensajeEvent?.Invoke("Éxito", "La cuenta ha sido eliminada correctamente.");
                    }
                }
                catch (Exception ex)
                {
                    MostrarMensajeEvent?.Invoke("Error", $"Error al eliminar la cuenta: {ex.Message}");
                }
            });

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
                using var db = _contextFactory.CreateDbContext();

                if (db.Database.CanConnect())
                {
                    var listaCuentas = db.Cuentas.Include(c => c.Saldos).OrderBy(c => c.Id).ToList();

                    foreach (var cuenta in listaCuentas)
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

                    // Calcular estadísticas
                    CuentasCount = listaCuentas.Count; // Total physical accounts
                    
                    var saldos = db.SaldosCuenta.ToList();
                    TotalDebito = saldos.Where(s => s.Saldo < 0).Sum(s => Math.Abs(s.Saldo));
                    TotalCredito = saldos.Where(s => s.Saldo > 0).Sum(s => s.Saldo);
                    PosicionNeta = TotalCredito - TotalDebito;
                }
                else
                {
                    Cuentas.Add(new CuentaResumenItem { Nombre = "ERROR: NO CONECTA A DB" });
                }
            }
            catch (Exception ex)
            {
                Cuentas.Add(new CuentaResumenItem { Nombre = $"Error: {ex.Message}" });
            }
        }
    }
}