using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;
using SistemaCambio.Services;
using SistemaCambio.Services.Validators;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class ArqueoWindow : Window
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ArqueoValidator _arqueoValidator;
        private ObservableCollection<ArqueoItem> _items = new();
        private bool _isInitializing = true;

        public ArqueoWindow()
        {
            _contextFactory = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            _arqueoValidator = App.Services.GetRequiredService<ArqueoValidator>();

            InitializeComponent();
            txtFecha.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            CargarDatos();
            _isInitializing = false;
        }

        private void CargarDatos()
        {
            _items.Clear();

            using var db = _contextFactory.CreateDbContext();

            var cajas = db.Cuentas.Where(c => c.Tipo == "Caja").OrderBy(c => c.Nombre).ToList();
            
            cmbCaja.Items.Clear();
            cmbCaja.Items.Add(new ComboBoxItem { Content = "TODAS", Tag = 0 });
            foreach (var caja in cajas)
            {
                cmbCaja.Items.Add(new ComboBoxItem { Content = caja.Nombre, Tag = caja.Id });
            }
            cmbCaja.SelectedIndex = 0;

            CargarArqueoItems(0);
        }

        private void CargarArqueoItems(int cajaId)
        {
            _items.Clear();

            using var db = _contextFactory.CreateDbContext();

            // Cargar saldos por moneda en vez de cuentas directas
            var query = db.SaldosCuenta
                .Include(s => s.Cuenta)
                .Where(s => s.Cuenta.Tipo == "Caja");

            if (cajaId > 0)
            {
                query = query.Where(s => s.CuentaId == cajaId);
                var caja = db.Cuentas.Find(cajaId);
                txtTitulo.Text = $"Arqueo - Caja: {caja?.Nombre ?? ""}";
            }
            else
            {
                txtTitulo.Text = "Arqueo - Caja: TODAS";
            }

            var saldos = query.OrderBy(s => s.Cuenta.Nombre).ThenBy(s => s.Moneda).ToList();

            foreach (var saldo in saldos)
            {
                _items.Add(new ArqueoItem
                {
                    CuentaId = saldo.CuentaId,
                    Codigo = saldo.Moneda,
                    Moneda = $"{saldo.Cuenta.Nombre} ({saldo.Moneda})",
                    SaldoSistema = saldo.Saldo,
                    SaldoArqueo = saldo.Saldo,
                    Diferencia = 0,
                    MonedaCodigo = saldo.Moneda,
                    NombreCuenta = saldo.Cuenta.Nombre
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

        private async void BtnAceptar_Click(object? sender, RoutedEventArgs e)
        {
            using var db = _contextFactory.CreateDbContext();

            var itemsConDiferencia = _items.Where(i => i.Diferencia != 0).ToList();

            // ═══ VALIDACIÓN DE DIFERENCIAS ═══
            foreach (var item in itemsConDiferencia)
            {
                var validacion = _arqueoValidator.ValidarArqueo(
                    item.NombreCuenta, item.MonedaCodigo, item.SaldoSistema, item.SaldoArqueo);

                if (validacion.HasErrors)
                {
                    NotificationService.Error($"Error en {item.Moneda}",
                        string.Join("\n", validacion.Errors.Select(err => $"• {err.Message}\n  {err.Details}")));
                    return;
                }

                if (validacion.HasWarnings)
                {
                    var mensajeWarnings = string.Join("\n\n",
                        validacion.Warnings.Select(w => $"⚠️ {w.Message}\n   {w.Details}"));
                    var continuar = await MostrarConfirmacion(
                        $"Advertencia - {item.Moneda}",
                        $"{mensajeWarnings}\n\n¿Desea continuar de todas formas?");
                    if (!continuar) return;
                }
            }

            // ═══ TODO VALIDADO — GUARDAR ═══
            foreach (var item in itemsConDiferencia)
            {
                var arqueo = new Arqueo
                {
                    Fecha = DateTime.Now,
                    CuentaId = item.CuentaId,
                    SaldoSistema = item.SaldoSistema,
                    SaldoArqueo = item.SaldoArqueo,
                    Diferencia = item.Diferencia,
                    Observaciones = item.Diferencia > 0 ? "Sobrante de caja" : "Faltante de caja"
                };

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

                var movimiento = new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = item.CuentaId,
                    Monto = item.Diferencia,
                    Fecha = DateTime.Now
                };
                db.Movimientos.Add(movimiento);

                // Actualizar saldo en SaldoCuenta
                var saldoCuenta = db.SaldosCuenta
                    .FirstOrDefault(s => s.CuentaId == item.CuentaId && s.Moneda == item.MonedaCodigo);
                if (saldoCuenta != null)
                    saldoCuenta.Saldo = item.SaldoArqueo;

                db.Arqueos.Add(arqueo);
                db.SaveChanges();

                arqueo.MovimientoAjusteId = movimiento.Id;
                db.SaveChanges();
            }

            decimal totalDiferencia = _items.Sum(i => i.Diferencia);
            int ajustesRealizados = itemsConDiferencia.Count;

            if (ajustesRealizados > 0)
            {
                if (totalDiferencia > 0)
                    Services.NotificationService.Warning("Arqueo completado", $"Sobrante: ${totalDiferencia:N2} ({ajustesRealizados} ajuste(s))");
                else if (totalDiferencia < 0)
                    Services.NotificationService.Warning("Arqueo completado", $"Faltante: ${Math.Abs(totalDiferencia):N2} ({ajustesRealizados} ajuste(s))");
                else
                    Services.NotificationService.Success("Arqueo completado", $"{ajustesRealizados} ajuste(s) realizado(s)");
            }
            else
            {
                Services.NotificationService.Success("Arqueo completado", "Sin diferencias - Caja cuadra perfectamente");
            }

            Close();
        }

        private async System.Threading.Tasks.Task<bool> MostrarConfirmacion(string titulo, string mensaje)
        {
            var dialog = new Window
            {
                Title = titulo,
                Width = 480,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = mensaje,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 440
            });
            var btnPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                Margin = new Avalonia.Thickness(0, 15, 0, 0),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            bool continuar = false;
            var btnContinuar = new Button { Content = "Continuar de todas formas" };
            var btnCancelar = new Button { Content = "Cancelar" };
            btnContinuar.Click += (s, ev) => { continuar = true; dialog.Close(); };
            btnCancelar.Click += (s, ev) => dialog.Close();
            btnPanel.Children.Add(btnContinuar);
            btnPanel.Children.Add(btnCancelar);
            panel.Children.Add(btnPanel);
            dialog.Content = panel;
            await dialog.ShowDialog(this);
            return continuar;
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
        public string MonedaCodigo { get; set; } = "";
        public string NombreCuenta { get; set; } = "";
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
