using Avalonia.Controls;
using Avalonia.Interactivity;
using SistemaCambio.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class ArqueoWindow : Window
    {
        private ObservableCollection<ArqueoItem> _items = new();
        private bool _isInitializing = true;

        public ArqueoWindow()
        {
            InitializeComponent();
            txtFecha.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            CargarDatos();
            _isInitializing = false;
        }

        private void CargarDatos()
        {
            _items.Clear();

            using var db = new AppDbContext();

            // Cargar cajas para el filtro
            var cajas = db.Cuentas.Where(c => c.Tipo == "Caja").OrderBy(c => c.Nombre).ToList();
            
            // Limpiar y recargar combo
            cmbCaja.Items.Clear();
            cmbCaja.Items.Add(new ComboBoxItem { Content = "TODAS", Tag = 0 });
            foreach (var caja in cajas)
            {
                cmbCaja.Items.Add(new ComboBoxItem { Content = caja.Nombre, Tag = caja.Id });
            }
            cmbCaja.SelectedIndex = 0;

            // Cargar todas las cuentas de tipo Caja agrupadas por moneda
            CargarArqueoItems(0);
        }

        private void CargarArqueoItems(int cajaId)
        {
            _items.Clear();

            using var db = new AppDbContext();

            var query = db.Cuentas.Where(c => c.Tipo == "Caja");
            
            if (cajaId > 0)
            {
                query = query.Where(c => c.Id == cajaId);
                var caja = db.Cuentas.Find(cajaId);
                txtTitulo.Text = $"Arqueo - Caja: {caja?.Nombre ?? ""}";
            }
            else
            {
                txtTitulo.Text = "Arqueo - Caja: TODAS";
            }

            var cuentas = query.OrderBy(c => c.Moneda).ToList();

            foreach (var cuenta in cuentas)
            {
                _items.Add(new ArqueoItem
                {
                    CuentaId = cuenta.Id,
                    Codigo = cuenta.Moneda,
                    Moneda = cuenta.Nombre,
                    SaldoSistema = cuenta.Saldo,
                    SaldoArqueo = cuenta.Saldo,  // Por defecto igual al sistema
                    Diferencia = 0
                });
            }

            dgArqueo.ItemsSource = _items;
            CalcularTotalDiferencia();
        }

        private void CmbCaja_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            
            var item = cmbCaja.SelectedItem as ComboBoxItem;
            if (item?.Tag is int cajaId)
            {
                CargarArqueoItems(cajaId);
            }
        }

        private void BtnRefrescar_Click(object? sender, RoutedEventArgs e)
        {
            var item = cmbCaja.SelectedItem as ComboBoxItem;
            if (item?.Tag is int cajaId)
            {
                CargarArqueoItems(cajaId);
            }
        }

        private void CalcularTotalDiferencia()
        {
            decimal total = _items.Sum(i => i.Diferencia);
            txtTotalDiferencia.Text = total.ToString("N2");
            txtTotalDiferencia.Foreground = total == 0 
                ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#238636"))
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#da3633"));
        }

        private void BtnAceptar_Click(object? sender, RoutedEventArgs e)
        {
            using var db = new AppDbContext();

            foreach (var item in _items.Where(i => i.Diferencia != 0))
            {
                // Crear registro de arqueo
                var arqueo = new Arqueo
                {
                    Fecha = DateTime.Now,
                    CuentaId = item.CuentaId,
                    SaldoSistema = item.SaldoSistema,
                    SaldoArqueo = item.SaldoArqueo,
                    Diferencia = item.Diferencia,
                    Observaciones = item.Diferencia > 0 ? "Sobrante de caja" : "Faltante de caja"
                };

                // Crear operación de ajuste
                var operacion = new Operacion
                {
                    Fecha = DateTime.Now,
                    TipoOperacion = item.Diferencia > 0 ? "Ajuste Sobrante" : "Ajuste Faltante",
                    MontoTotalOrigen = 0,
                    MontoTotalDestino = Math.Abs(item.Diferencia),
                    CotizacionAplicada = 1,
                    Observaciones = $"Arqueo de caja - {arqueo.Observaciones}"
                };
                db.Operaciones.Add(operacion);

                // Crear movimiento de ajuste
                var movimiento = new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = item.CuentaId,
                    Monto = item.Diferencia,  // Positivo si sobrante, negativo si faltante
                    Fecha = DateTime.Now
                };
                db.Movimientos.Add(movimiento);

                // Actualizar saldo de la cuenta
                var cuenta = db.Cuentas.Find(item.CuentaId);
                if (cuenta != null)
                {
                    cuenta.Saldo = item.SaldoArqueo;
                }

                // Guardar arqueo con referencia al movimiento
                db.Arqueos.Add(arqueo);
                db.SaveChanges();

                // Actualizar referencia del arqueo al movimiento
                arqueo.MovimientoAjusteId = movimiento.Id;
                db.SaveChanges();
            }

            Close();
        }

        private void BtnSalir_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Clase auxiliar para el DataGrid
    public class ArqueoItem
    {
        public int CuentaId { get; set; }
        public string Codigo { get; set; } = "";
        public string Moneda { get; set; } = "";
        public decimal SaldoSistema { get; set; }
        
        private decimal _saldoArqueo;
        public decimal SaldoArqueo 
        { 
            get => _saldoArqueo;
            set
            {
                _saldoArqueo = value;
                Diferencia = _saldoArqueo - SaldoSistema;
            }
        }
        
        public decimal Diferencia { get; set; }
    }
}
