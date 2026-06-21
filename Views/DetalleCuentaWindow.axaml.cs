using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;

namespace SistemaCambio.Views
{
    public partial class DetalleCuentaWindow : Window
    {
        private readonly ICasaCambioApiClient? _apiClient;
        private readonly int _cuentaId;

        public ObservableCollection<DetalleSaldoItem> Saldos { get; set; } = new();
        public ObservableCollection<MovimientoDisplay> Movimientos { get; set; } = new();

        public DetalleCuentaWindow()
        {
            InitializeComponent();
        }

        public DetalleCuentaWindow(int cuentaId)
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            _cuentaId = cuentaId;
            InitializeComponent();
            CargarDatosAsync();
        }

        private async void CargarDatosAsync()
        {
            try
            {
                if (_apiClient == null) return;

                var cuentas = await _apiClient.ObtenerCuentasAsync();
                var cuenta = cuentas.FirstOrDefault(c => c.Id == _cuentaId);

                if (cuenta != null)
                {
                    txtNombreCuenta.Text = cuenta.Nombre;
                    txtTipoCuenta.Text = cuenta.Tipo;

                    string[] colors = { "#3B82F6", "#16A34A", "#D97706", "#DC2626", "#8b5cf6", "#14b8a6" };
                    int brushIndex = 0;
                    foreach (var saldo in cuenta.Saldos.OrderBy(s => s.Moneda))
                    {
                        var brush = new SolidColorBrush(Color.Parse(colors[brushIndex % colors.Length]));
                        Saldos.Add(new DetalleSaldoItem { Moneda = saldo.Moneda, SaldoFormatted = $"{saldo.Saldo:N2}", ColorBrush = brush });
                        brushIndex++;
                    }
                    icSaldos.ItemsSource = Saldos;

                    var movimientosPage = await _apiClient.ObtenerMovimientosCuentaAsync(_cuentaId);
                    var dangerBrush = (ISolidColorBrush)this.FindResource("DangerBrush")!;
                    var successBrush = (ISolidColorBrush)this.FindResource("SuccessBrush")!;
                    var fgBrush = (ISolidColorBrush)this.FindResource("PrimaryTextBrush")!;

                    foreach (var m in movimientosPage.Items)
                    {
                        string prefijo = m.Monto > 0 ? "+" : "";
                        var color = m.Monto > 0 ? successBrush : (m.Monto < 0 ? dangerBrush : fgBrush);
                        Movimientos.Add(new MovimientoDisplay
                        {
                            Fecha = m.Fecha,
                            CodigoOperacion = $"OP-{m.OperacionId:D5}",
                            Moneda = m.Moneda,
                            MontoFormatted = $"{prefijo}{m.Monto:N2}",
                            MontoColor = color
                        });
                    }
                    dgMovimientos.ItemsSource = Movimientos;
                    if (Movimientos.Count == 0) txtSinMovimientos.IsVisible = true;
                }
            }
            catch (Exception ex) { AppLogger.Warn("CargarDatosAsync", ex); }
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();
    }

    public class MovimientoDisplay
    {
        public DateTime Fecha { get; set; }
        public string CodigoOperacion { get; set; } = "";
        public string Moneda { get; set; } = "";
        public string MontoFormatted { get; set; } = "";
        public ISolidColorBrush? MontoColor { get; set; }
    }

    public class DetalleSaldoItem
    {
        public string Moneda { get; set; } = "";
        public string SaldoFormatted { get; set; } = "";
        public ISolidColorBrush? ColorBrush { get; set; }
    }
}
