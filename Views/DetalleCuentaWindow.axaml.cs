using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;

namespace SistemaCambio.Views
{
    public partial class DetalleCuentaWindow : Window
    {
        private readonly IDbContextFactory<AppDbContext>? _contextFactory;
        private readonly int _cuentaId;
        
        public ObservableCollection<DetalleSaldoItem> Saldos { get; set; } = new();
        public ObservableCollection<MovimientoDisplay> Movimientos { get; set; } = new();

        public DetalleCuentaWindow()
        {
            InitializeComponent();
        }

        public DetalleCuentaWindow(int cuentaId)
        {
            _contextFactory = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            _cuentaId = cuentaId;
            
            InitializeComponent();
            
            CargarDatos();
        }

        private async void CargarDatos()
        {
            try
            {
                if (_contextFactory == null) return;
                using var db = await _contextFactory.CreateDbContextAsync();
                
                var cuenta = await db.Cuentas
                    .Include(c => c.Saldos)
                    .FirstOrDefaultAsync(c => c.Id == _cuentaId);

                if (cuenta != null)
                {
                    txtNombreCuenta.Text = cuenta.Nombre;
                    txtTipoCuenta.Text = cuenta.Tipo;

                    // Cargar Saldos
                    string[] colors = { "#3B82F6", "#16A34A", "#D97706", "#DC2626", "#8b5cf6", "#14b8a6" };
                    int brushIndex = 0;
                    
                    var saldosOrdenados = cuenta.Saldos.OrderBy(s => s.Moneda).ToList();
                    foreach (var saldo in saldosOrdenados)
                    {
                        var brush = new SolidColorBrush(Color.Parse(colors[brushIndex % colors.Length]));
                        Saldos.Add(new DetalleSaldoItem
                        {
                            Moneda = saldo.Moneda,
                            SaldoFormatted = $"{saldo.Saldo:N2}",
                            ColorBrush = brush
                        });
                        brushIndex++;
                    }
                    icSaldos.ItemsSource = Saldos;

                    // Cargar últimos movimientos (ej limitamos a 100 para rendimiento)
                    var movimientosDb = await db.Movimientos
                        .Include(m => m.Operacion)
                        .Where(m => m.CuentaId == _cuentaId)
                        .OrderByDescending(m => m.Fecha)
                        .Take(100)
                        .ToListAsync();

                    var dangerBrush = (ISolidColorBrush)this.FindResource("DangerBrush")!;
                    var successBrush = (ISolidColorBrush)this.FindResource("SuccessBrush")!;
                    var fgBrush = (ISolidColorBrush)this.FindResource("PrimaryTextBrush")!;

                    foreach (var m in movimientosDb)
                    {
                        string prefijo = m.Monto > 0 ? "+" : "";
                        var color = m.Monto > 0 ? successBrush : (m.Monto < 0 ? dangerBrush : fgBrush);

                        Movimientos.Add(new MovimientoDisplay
                        {
                            Fecha = m.Fecha,
                            TipoOperacion = m.Operacion?.TipoOperacion ?? "Manual",
                            Moneda = m.Moneda,
                            MontoFormatted = $"{prefijo}{m.Monto:N2}",
                            MontoColor = color,
                            Detalle = string.IsNullOrWhiteSpace(m.Operacion?.Observaciones) ? "Sin detalles" : m.Operacion.Observaciones
                        });
                    }

                    dgMovimientos.ItemsSource = Movimientos;
                    
                    if (Movimientos.Count == 0)
                    {
                        txtSinMovimientos.IsVisible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando detalle de cuenta: {ex.Message}");
            }
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class MovimientoDisplay
    {
        public DateTime Fecha { get; set; }
        public string TipoOperacion { get; set; } = "";
        public string Moneda { get; set; } = "";
        public string MontoFormatted { get; set; } = "";
        public ISolidColorBrush? MontoColor { get; set; }
        public string Detalle { get; set; } = "";
    }

    public class DetalleSaldoItem
    {
        public string Moneda { get; set; } = "";
        public string SaldoFormatted { get; set; } = "";
        public ISolidColorBrush? ColorBrush { get; set; }
    }
}
