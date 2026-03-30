using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class DetalleMovimientosWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private ObservableCollection<MovimientoDetalle> _movimientos = new();
        private System.Collections.Generic.List<CasaCambio.Shared.DTOs.CuentaDto> _cuentasCache = new();

        public DetalleMovimientosWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
            CargarDatosAsync();
        }

        private async void CargarDatosAsync()
        {
            try
            {
                _cuentasCache = await _apiClient.ObtenerCuentasAsync();

                cmbCuenta.Items.Add(new ComboBoxItem { Content = "Todas", Tag = 0 });
                cmbCuentaExterna.Items.Add(new ComboBoxItem { Content = "Todas", Tag = 0 });
                foreach (var cuenta in _cuentasCache.OrderBy(c => c.Nombre))
                {
                    cmbCuenta.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = cuenta.Id });
                    cmbCuentaExterna.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = cuenta.Id });
                }
                cmbCuenta.SelectedIndex = 0;
                cmbCuentaExterna.SelectedIndex = 0;

                var monedas = _cuentasCache.SelectMany(c => c.Saldos).Select(s => s.Moneda).Distinct().OrderBy(m => m).ToList();
                foreach (var moneda in monedas)
                    cmbMoneda.Items.Add(new ComboBoxItem { Content = moneda, Tag = moneda });
                if (cmbMoneda.Items.Count > 0) cmbMoneda.SelectedIndex = 0;

                dgMovimientos.ItemsSource = _movimientos;
            }
            catch (Exception ex) { AppLogger.Warn("CargarDatosAsync", ex); }
        }

        private async void BtnBuscar_Click(object? sender, RoutedEventArgs e)
        {
            _movimientos.Clear();

            var itemCuenta = cmbCuenta.SelectedItem as ComboBoxItem;
            int cuentaId = itemCuenta?.Tag is int id ? id : 0;

            DateTime fechaDesde = DateTime.SpecifyKind(dpDesde.SelectedDate?.DateTime ?? DateTime.Today, DateTimeKind.Utc);
            DateTime fechaHasta = DateTime.SpecifyKind((dpHasta.SelectedDate?.DateTime ?? DateTime.Today).AddDays(1), DateTimeKind.Utc);

            try
            {
                if (cuentaId > 0)
                {
                    var movimientos = await _apiClient.ObtenerMovimientosCuentaAsync(cuentaId, fechaDesde, fechaHasta);
                    foreach (var mov in movimientos)
                    {
                        _movimientos.Add(new MovimientoDetalle
                        {
                            Id = mov.Id, Fecha = mov.Fecha, TipoOperacion = "",
                            CuentaNombre = mov.NombreCuenta, Moneda = mov.Moneda,
                            Debito = mov.Monto < 0 ? Math.Abs(mov.Monto) : 0,
                            Credito = mov.Monto > 0 ? mov.Monto : 0, Observaciones = ""
                        });
                    }
                }
                else
                {
                    // All accounts: get operations instead
                    var ops = await _apiClient.ObtenerOperacionesAsync(fechaDesde, fechaHasta, pageSize: 200);
                    foreach (var op in ops.Items)
                    {
                        foreach (var mov in op.Movimientos)
                        {
                            _movimientos.Add(new MovimientoDetalle
                            {
                                Id = mov.Id, Fecha = mov.Fecha, TipoOperacion = op.TipoOperacion,
                                CuentaNombre = mov.NombreCuenta, Moneda = mov.Moneda,
                                Debito = mov.Monto < 0 ? Math.Abs(mov.Monto) : 0,
                                Credito = mov.Monto > 0 ? mov.Monto : 0, Observaciones = op.Observaciones ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.Warn("BtnBuscar_Click", ex); }

            txtResultados.Text = $"{_movimientos.Count} movimiento(s) encontrado(s)";
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
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
