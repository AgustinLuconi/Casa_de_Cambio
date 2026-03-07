using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;
using SistemaCambio.Services;
using System.Collections.Generic;
using System.Linq;

namespace SistemaCambio.Views
{
    /// <summary>
    /// Item para el DataGrid de saldos iniciales al crear una cuenta.
    /// </summary>
    public class SaldoInicialItem
    {
        public string Moneda { get; set; } = "";
        public string Nombre { get; set; } = "";
        public decimal Saldo { get; set; }
    }

    public partial class NuevaCuentaWindow : Window
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private List<SaldoInicialItem> _saldosIniciales = new();
        private int? _cuentaIdAEditar;

        public NuevaCuentaWindow()
        {
            _contextFactory = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

            InitializeComponent();
            CargarMonedas();
        }

        public NuevaCuentaWindow(int cuentaId) : this()
        {
            _cuentaIdAEditar = cuentaId;
            CargarDatosCuentaEdicion();
        }

        private void CargarMonedas()
        {
            using var db = _contextFactory.CreateDbContext();

            var monedas = db.Monedas.Where(m => m.Activa).OrderBy(m => m.Codigo).ToList();

            if (!monedas.Any())
            {
                // Fallback si no hay monedas configuradas
                monedas = new List<Moneda>
                {
                    new() { Codigo = "ARS", Nombre = "Peso Argentino" },
                    new() { Codigo = "USD", Nombre = "Dólar" },
                    new() { Codigo = "EUR", Nombre = "Euro" }
                };
            }

            _saldosIniciales = monedas.Select(m => new SaldoInicialItem
            {
                Moneda = m.Codigo,
                Nombre = m.Nombre,
                Saldo = 0m
            }).ToList();

            dgSaldosIniciales.ItemsSource = _saldosIniciales;
        }

        private void CargarDatosCuentaEdicion()
        {
            if (_cuentaIdAEditar == null) return;

            Title = "Editar Cuenta";
            txtTitulo.Text = "Editar Cuenta";
            txtSaldosTitulo.Text = "Saldos Actuales por Divisa";
            iconHeader.Kind = Material.Icons.MaterialIconKind.BankTransfer;

            using var db = _contextFactory.CreateDbContext();
            var cuenta = db.Cuentas.Include(c => c.Saldos).FirstOrDefault(c => c.Id == _cuentaIdAEditar);

            if (cuenta != null)
            {
                txtNombre.Text = cuenta.Nombre;
                
                // Set the correct Tipo
                for (int i = 0; i < cmbTipo.Items.Count; i++)
                {
                    if (cmbTipo.Items[i] is ComboBoxItem item && item.Content?.ToString() == cuenta.Tipo)
                    {
                        cmbTipo.SelectedIndex = i;
                        break;
                    }
                }

                // Update loaded balances
                foreach (var saldoDB in cuenta.Saldos)
                {
                    var saldoLocal = _saldosIniciales.FirstOrDefault(s => s.Moneda == saldoDB.Moneda);
                    if (saldoLocal != null)
                    {
                        saldoLocal.Saldo = saldoDB.Saldo;
                    }
                    else
                    {
                        // En caso de que haya un saldo en una moneda inactiva (raro pero posible)
                        _saldosIniciales.Add(new SaldoInicialItem
                        {
                            Moneda = saldoDB.Moneda,
                            Nombre = saldoDB.Moneda,
                            Saldo = saldoDB.Saldo
                        });
                    }
                }
                
                dgSaldosIniciales.ItemsSource = null;
                dgSaldosIniciales.ItemsSource = _saldosIniciales;
            }
        }

        private decimal ParsearMonto(string? texto)
        {
            return MontoHelper.Parsear(texto);
        }

        private void BtnGuardar_Click(object? sender, RoutedEventArgs e)
        {
            string nombre = txtNombre.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(nombre))
            {
                return;
            }

            var itemTipo = cmbTipo.SelectedItem as ComboBoxItem;
            string tipo = itemTipo?.Content?.ToString() ?? "Caja";

            using var db = _contextFactory.CreateDbContext();

            if (_cuentaIdAEditar.HasValue)
            {
                // Modo Edición
                var cuentaDB = db.Cuentas.Include(c => c.Saldos).FirstOrDefault(c => c.Id == _cuentaIdAEditar.Value);
                if (cuentaDB != null)
                {
                    cuentaDB.Nombre = nombre;
                    cuentaDB.Tipo = tipo;

                    // Update balances
                    foreach (var item in _saldosIniciales)
                    {
                        var saldoExistente = cuentaDB.Saldos.FirstOrDefault(s => s.Moneda == item.Moneda);
                        if (saldoExistente != null)
                        {
                            saldoExistente.Saldo = item.Saldo;
                        }
                        else
                        {
                            db.SaldosCuenta.Add(new SaldoCuenta
                            {
                                CuentaId = cuentaDB.Id,
                                Moneda = item.Moneda,
                                Saldo = item.Saldo
                            });
                        }
                    }
                    db.SaveChanges();
                }
            }
            else
            {
                // Modo Creación
                var cuenta = new Cuenta
                {
                    Nombre = nombre,
                    Tipo = tipo
                };

                db.Cuentas.Add(cuenta);
                db.SaveChanges();

                // Crear SaldoCuenta para cada divisa
                foreach (var item in _saldosIniciales)
                {
                    db.SaldosCuenta.Add(new SaldoCuenta
                    {
                        CuentaId = cuenta.Id,
                        Moneda = item.Moneda,
                        Saldo = item.Saldo
                    });
                }

                db.SaveChanges();
            }

            Close();
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
