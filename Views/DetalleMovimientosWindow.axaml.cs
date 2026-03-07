using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class DetalleMovimientosWindow : Window
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private ObservableCollection<MovimientoDetalle> _movimientos = new();

        public DetalleMovimientosWindow()
        {
            _contextFactory = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

            InitializeComponent();
            
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
            
            CargarDatos();
        }

        private void CargarDatos()
        {
            using var db = _contextFactory.CreateDbContext();

            cmbCuenta.Items.Add(new ComboBoxItem { Content = "Todas", Tag = 0 });
            cmbCuentaExterna.Items.Add(new ComboBoxItem { Content = "Todas", Tag = 0 });

            var cuentas = db.Cuentas.OrderBy(c => c.Nombre).ToList();
            foreach (var cuenta in cuentas)
            {
                cmbCuenta.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = cuenta.Id });
                cmbCuentaExterna.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = cuenta.Id });
            }

            cmbCuenta.SelectedIndex = 0;
            cmbCuentaExterna.SelectedIndex = 0;

            var monedas = db.SaldosCuenta.Select(s => s.Moneda).Distinct().OrderBy(m => m).ToList();
            foreach (var moneda in monedas)
            {
                cmbMoneda.Items.Add(new ComboBoxItem { Content = moneda, Tag = moneda });
            }
            cmbMoneda.SelectedIndex = 0;

            dgMovimientos.ItemsSource = _movimientos;
        }

        private void BtnBuscar_Click(object? sender, RoutedEventArgs e)
        {
            _movimientos.Clear();

            using var db = _contextFactory.CreateDbContext();

            var itemCuenta = cmbCuenta.SelectedItem as ComboBoxItem;
            var itemMoneda = cmbMoneda.SelectedItem as ComboBoxItem;
            
            int cuentaId = itemCuenta?.Tag is int id ? id : 0;
            string? monedaFiltro = itemMoneda?.Tag as string;

            DateTime fechaDesde = DateTime.SpecifyKind(dpDesde.SelectedDate?.DateTime ?? DateTime.Today, DateTimeKind.Utc);
            DateTime fechaHasta = DateTime.SpecifyKind((dpHasta.SelectedDate?.DateTime ?? DateTime.Today).AddDays(1), DateTimeKind.Utc);

            var query = db.Movimientos
                .Include(m => m.Operacion)
                .Include(m => m.Cuenta)
                .Where(m => m.Fecha >= fechaDesde && m.Fecha < fechaHasta);

            if (cuentaId > 0)
            {
                query = query.Where(m => m.CuentaId == cuentaId);
            }

            if (!string.IsNullOrEmpty(monedaFiltro))
            {
                query = query.Where(m => m.Moneda == monedaFiltro);
            }

            var movimientos = query.OrderByDescending(m => m.Fecha).ToList();

            foreach (var mov in movimientos)
            {
                _movimientos.Add(new MovimientoDetalle
                {
                    Id = mov.Id,
                    Fecha = mov.Fecha,
                    TipoOperacion = mov.Operacion?.TipoOperacion ?? "",
                    CuentaNombre = mov.Cuenta?.Nombre ?? "",
                    Moneda = mov.Moneda,
                    Debito = mov.Monto < 0 ? Math.Abs(mov.Monto) : 0,
                    Credito = mov.Monto > 0 ? mov.Monto : 0,
                    Observaciones = mov.Operacion?.Observaciones ?? ""
                });
            }

            txtResultados.Text = $"{_movimientos.Count} movimiento(s) encontrado(s)";
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class MovimientoDetalle
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string TipoOperacion { get; set; } = "";
        public string CuentaNombre { get; set; } = "";
        public string Moneda { get; set; } = "";
        public decimal Debito { get; set; }
        public decimal Credito { get; set; }
        public string Observaciones { get; set; } = "";
    }
}
