using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Services;
using System;

using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace SistemaCambio.Views
{
    public class SaldoCajaItem
    {
        public string Nombre { get; set; } = "";
        public string SaldoFormatted { get; set; } = "";
        public Avalonia.Media.IBrush ColorBrush { get; set; } = Avalonia.Media.Brushes.Gray;
    }

    public partial class CierreCajaWindow : Window
    {
        private readonly ICierreCajaService _cierreCajaService;
        private int? _cierreId;

        public CierreCajaWindow()
        {
            _cierreCajaService = App.Services.GetRequiredService<ICierreCajaService>();

            InitializeComponent();
            txtFecha.Text = DateTime.Today.ToString("dddd, dd 'de' MMMM 'de' yyyy");
            
            CargarSaldosDinamicos();
            CargarCierreExistente();
        }

        private void CargarCierreExistente()
        {
            var cierre = _cierreCajaService.ObtenerCierreDeHoy();
            
            if (cierre != null)
            {
                MostrarCierre(cierre);
            }
        }

        private void BtnGenerar_Click(object? sender, RoutedEventArgs e)
        {
            var resultado = _cierreCajaService.GenerarCierre(
                txtObservaciones.Text ?? "");

            if (resultado.Exitoso && resultado.Cierre != null)
            {
                MostrarCierre(resultado.Cierre);
                
                borderEstado.IsVisible = true;
                btnCerrarDefinitivo.IsEnabled = true;
                
                NotificationService.Info("Cierre generado", "Revise los datos y cierre definitivamente");
            }
            else
            {
                NotificationService.Error("Error al generar cierre", resultado.Mensaje);
            }
        }

        private void MostrarCierre(Models.CierreCaja cierre)
        {
            _cierreId = cierre.Id;

            txtCantidadCompras.Text = cierre.CantidadCompras.ToString();
            txtComprasUSD.Text = $"${cierre.TotalComprasUSD:N2}";
            txtComprasARS.Text = $"${cierre.TotalComprasARS:N2}";

            txtCantidadVentas.Text = cierre.CantidadVentas.ToString();
            txtVentasUSD.Text = $"${cierre.TotalVentasUSD:N2}";
            txtVentasARS.Text = $"${cierre.TotalVentasARS:N2}";

            txtTotalDiferencias.Text = $"${cierre.TotalDiferencias:N2}";
            
            if (cierre.TotalDiferencias == 0)
            {
                txtTotalDiferencias.Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#16A34A"));
            }
            else
            {
                txtTotalDiferencias.Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#DC2626"));
            }

            CargarSaldosDinamicos();

            borderEstado.IsVisible = true;
            
            if (cierre.Cerrado)
            {
                borderEstado.Background = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#16A34A20"));
                borderEstado.BorderBrush = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#16A34A"));
                txtEstado.Text = "✓ Cierre cerrado definitivamente";
                txtEstado.Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#16A34A"));
                
                btnCerrarDefinitivo.IsEnabled = false;
                btnGenerar.IsEnabled = false;
                txtObservaciones.IsReadOnly = true;
            }
            else
            {
                btnCerrarDefinitivo.IsEnabled = true;
            }
        }

        private void CargarSaldosDinamicos()
        {
            try
            {
                using var db = App.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<SistemaCambio.Models.AppDbContext>>().CreateDbContext();
                
                var monedasActivas = db.Monedas.Where(m => m.Activa).Select(m => m.Codigo).ToList();
                
                var saldosGrouped = db.SaldosCuenta
                    .Include(s => s.Cuenta)
                    .Where(s => s.Cuenta.Tipo == "Caja")
                    .GroupBy(s => s.Moneda)
                    .Select(g => new { Moneda = g.Key, Saldo = g.Sum(x => x.Saldo) })
                    .ToList();
                
                var items = new System.Collections.Generic.List<SaldoCajaItem>();
                
                string[] colors = { "#3B82F6", "#16A34A", "#D97706", "#DC2626", "#8b5cf6", "#14b8a6" };
                int brushIndex = 0;
                
                var todasLasMonedas = monedasActivas.Union(saldosGrouped.Select(s => s.Moneda))
                    .Union(new[] { "ARS", "USD", "EUR" })
                    .Distinct();

                var codigosEspeciales = new[] { "ARS", "USD", "EUR" };
                var ordenados = todasLasMonedas.OrderBy(x => 
                {
                    var idx = System.Array.IndexOf(codigosEspeciales, x);
                    return idx == -1 ? 99 : idx;
                }).ThenBy(x => x);

                foreach (var moneda in ordenados)
                {
                    var saldoObj = saldosGrouped.FirstOrDefault(s => s.Moneda == moneda);
                    decimal saldoMonto = saldoObj?.Saldo ?? 0m;
                    
                    var brush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(colors[brushIndex % colors.Length]));
                    
                    string nombreLargo = moneda switch {
                        "ARS" => "PESOS (ARS)",
                        "USD" => "DÓLARES (USD)",
                        "EUR" => "EUROS (EUR)",
                        "BRL" => "REALES (BRL)",
                        _ => moneda
                    };
                    
                    items.Add(new SaldoCajaItem
                    {
                        Nombre = nombreLargo,
                        SaldoFormatted = $"${saldoMonto:N2}",
                        ColorBrush = brush
                    });
                    brushIndex++;
                }
                
                icSaldos.ItemsSource = items;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private async void BtnCerrarDefinitivo_Click(object? sender, RoutedEventArgs e)
        {
            if (_cierreId == null) return;

            var confirma = await MostrarConfirmacion(
                "¿Cerrar el día definitivamente?",
                "⚠️ Esta acción NO se puede deshacer.\n\n" +
                "Una vez cerrado:\n" +
                "• No se pueden agregar más operaciones a este día\n" +
                "• Los datos quedan bloqueados para auditoría");

            if (!confirma) return;

            var resultado = _cierreCajaService.CerrarDefinitivo(_cierreId.Value);

            if (resultado.Exitoso && resultado.Cierre != null)
            {
                MostrarCierre(resultado.Cierre);
                NotificationService.CierreCajaCompletado();
            }
            else
            {
                NotificationService.Error("Error al cerrar", resultado.Mensaje);
            }
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async System.Threading.Tasks.Task<bool> MostrarConfirmacion(
            string titulo, string mensaje)
        {
            var dialog = new Window
            {
                Title = titulo,
                Width = 450,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#161b22"))
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
            panel.Children.Add(new TextBlock 
            { 
                Text = mensaje, 
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#e6edf3"))
            });

            var btnPanel = new StackPanel 
            { 
                Orientation = Avalonia.Layout.Orientation.Horizontal, 
                Spacing = 10, 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };

            bool resultado = false;
            var btnSi = new Button 
            { 
                Content = "Sí, cerrar definitivamente", 
                Width = 180,
                Background = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#da3633")),
                Foreground = Avalonia.Media.Brushes.White
            };
            var btnNo = new Button 
            { 
                Content = "Cancelar", 
                Width = 100,
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#e6edf3"))
            };

            btnSi.Click += (s, ev) => { resultado = true; dialog.Close(); };
            btnNo.Click += (s, ev) => { resultado = false; dialog.Close(); };

            btnPanel.Children.Add(btnSi);
            btnPanel.Children.Add(btnNo);
            panel.Children.Add(btnPanel);

            dialog.Content = panel;
            await dialog.ShowDialog(this);

            return resultado;
        }

        private async System.Threading.Tasks.Task MostrarMensaje(string titulo, string mensaje)
        {
            var dialog = new Window
            {
                Title = titulo,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#161b22"))
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
            panel.Children.Add(new TextBlock 
            { 
                Text = mensaje, 
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#e6edf3"))
            });

            var btnOk = new Button 
            { 
                Content = "OK", 
                Width = 100, 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Background = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#238636")),
                Foreground = Avalonia.Media.Brushes.White
            };
            btnOk.Click += (s, ev) => dialog.Close();

            panel.Children.Add(btnOk);
            dialog.Content = panel;

            await dialog.ShowDialog(this);
        }

        private async void MostrarError(string mensaje)
        {
            await MostrarMensaje("Error", mensaje);
        }
    }
}
