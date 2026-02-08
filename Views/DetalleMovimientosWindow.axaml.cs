using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class DetalleMovimientosWindow : Window
    {
        private ObservableCollection<MovimientoDetalle> _movimientos = new();

        public DetalleMovimientosWindow()
        {
            InitializeComponent();
            
            // Establecer fechas por defecto
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
            
            CargarDatos();
        }

        private void CargarDatos()
        {
            using var db = new AppDbContext();

            // Cargar cuentas
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

            // Cargar monedas
            var monedas = cuentas.Select(c => c.Moneda).Distinct().ToList();
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

            using var db = new AppDbContext();

            // Obtener filtros
            var itemCuenta = cmbCuenta.SelectedItem as ComboBoxItem;
            var itemMoneda = cmbMoneda.SelectedItem as ComboBoxItem;
            
            int cuentaId = itemCuenta?.Tag is int id ? id : 0;
            string? monedaFiltro = itemMoneda?.Tag as string;

            DateTime fechaDesde = dpDesde.SelectedDate?.DateTime ?? DateTime.Today;
            DateTime fechaHasta = dpHasta.SelectedDate?.DateTime.AddDays(1) ?? DateTime.Today.AddDays(1);

            // Query base
            var query = db.Movimientos
                .Include(m => m.Operacion)
                .Include(m => m.Cuenta)
                .Where(m => m.Fecha >= fechaDesde && m.Fecha < fechaHasta);

            // Filtrar por cuenta
            if (cuentaId > 0)
            {
                query = query.Where(m => m.CuentaId == cuentaId);
            }

            // Filtrar por moneda
            if (!string.IsNullOrEmpty(monedaFiltro))
            {
                query = query.Where(m => m.Cuenta.Moneda == monedaFiltro);
            }

            // Ejecutar query
            var movimientos = query.OrderByDescending(m => m.Fecha).ToList();

            foreach (var mov in movimientos)
            {
                _movimientos.Add(new MovimientoDetalle
                {
                    Id = mov.Id,
                    Fecha = mov.Fecha,
                    TipoOperacion = mov.Operacion?.TipoOperacion ?? "",
                    CuentaNombre = mov.Cuenta?.Nombre ?? "",
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

    // Clase auxiliar para el DataGrid
    public class MovimientoDetalle
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string TipoOperacion { get; set; } = "";
        public string CuentaNombre { get; set; } = "";
        public decimal Debito { get; set; }
        public decimal Credito { get; set; }
        public string Observaciones { get; set; } = "";
    }
}
