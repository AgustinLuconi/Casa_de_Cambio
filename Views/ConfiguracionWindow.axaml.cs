using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;
using SistemaCambio.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class ConfiguracionWindow : Window
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ConfiguracionWindow()
        {
            _contextFactory = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

            InitializeComponent();
            dpFechaCotizacion.SelectedDate = new DateTimeOffset(DateTime.Today);
            CargarMonedas();
            CargarMonedaCombo();
        }

        private void ToggleTema_Changed(object? sender, RoutedEventArgs e)
        {
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = toggleTema.IsChecked == true 
                    ? ThemeVariant.Dark 
                    : ThemeVariant.Light;
            }
        }

        private void CargarMonedas()
        {
            using var db = _contextFactory.CreateDbContext();
            var monedas = db.Monedas.OrderBy(m => m.Codigo).ToList();
            dgMonedas.ItemsSource = monedas;
        }

        private void CargarMonedaCombo()
        {
            using var db = _contextFactory.CreateDbContext();
            var monedas = db.Monedas.Where(m => m.Activa).ToList();
            
            cmbMonedaCotiz.Items.Clear();
            foreach (var moneda in monedas)
            {
                cmbMonedaCotiz.Items.Add(new ComboBoxItem { Content = moneda.Codigo, Tag = moneda.Id });
            }
            
            if (cmbMonedaCotiz.Items.Count > 0)
                cmbMonedaCotiz.SelectedIndex = 0;
        }

        private async void BtnNuevaMoneda_Click(object? sender, RoutedEventArgs e)
        {
            var codigo = txtNuevoCodigo.Text?.Trim().ToUpper();
            var nombre = txtNuevoNombre.Text?.Trim();

            if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(nombre))
            {
                await MostrarMensaje("Error", "Debe ingresar el código y el nombre de la moneda.");
                return;
            }

            using var db = _contextFactory.CreateDbContext();
            
            // Verificar si ya existe
            if (db.Monedas.Any(m => m.Codigo == codigo))
            {
                await MostrarMensaje("Error", $"La moneda {codigo} ya existe en el sistema.");
                return;
            }

            var nuevaMoneda = new Moneda
            {
                Codigo = codigo,
                Nombre = nombre,
                Activa = true
            };
            
            db.Monedas.Add(nuevaMoneda);
            await db.SaveChangesAsync();
            
            // Limpiar inputs
            txtNuevoCodigo.Text = "";
            txtNuevoNombre.Text = "";
            
            CargarMonedas();
            CargarMonedaCombo();
        }

        private async void BtnGuardarCambios_Click(object? sender, RoutedEventArgs e)
        {
            var monedasEnVista = dgMonedas.ItemsSource as List<Moneda>;
            if (monedasEnVista == null) return;

            using var db = _contextFactory.CreateDbContext();
            
            // Adjuntar o actualizar las entidades
            foreach (var moneda in monedasEnVista)
            {
                var existente = await db.Monedas.FindAsync(moneda.Id);
                if (existente != null)
                {
                    existente.Codigo = moneda.Codigo;
                    existente.Nombre = moneda.Nombre;
                    existente.Activa = moneda.Activa;
                }
            }
            
            await db.SaveChangesAsync();
            CargarMonedaCombo(); // Actualizar combo de cotizaciones por si cambió algo
            await MostrarMensaje("Éxito", "Cambios guardados correctamente.");
        }

        private void BtnRefrescarMonedas_Click(object? sender, RoutedEventArgs e)
        {
            CargarMonedas();
        }

        private async void BtnEliminarMoneda_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Moneda moneda)
            {
                using var db = _contextFactory.CreateDbContext();
                
                // Verificar si está en uso en Cuentas o Cotizaciones
                bool usadaSaldos = await db.SaldosCuenta.AnyAsync(s => s.Moneda == moneda.Codigo);
                bool usadaCotizaciones = await db.CotizacionesDiarias.AnyAsync(c => c.MonedaId == moneda.Id);
                
                if (usadaSaldos || usadaCotizaciones)
                {
                    await MostrarMensaje("Eliminación denegada", $"No se puede eliminar la moneda {moneda.Codigo} porque ya tiene saldo en cuentas o cotizaciones asociadas.");
                    return;
                }
                
                var existente = await db.Monedas.FindAsync(moneda.Id);
                if (existente != null)
                {
                    db.Monedas.Remove(existente);
                    await db.SaveChangesAsync();
                    CargarMonedas();
                    CargarMonedaCombo();
                }
            }
        }

        private void BtnCargarCotizaciones_Click(object? sender, RoutedEventArgs e)
        {
            using var db = _contextFactory.CreateDbContext();
            
            DateTime fecha = dpFechaCotizacion.SelectedDate?.Date ?? DateTime.Today;
            
            var cotizaciones = db.CotizacionesDiarias
                .Include(c => c.Moneda)
                .Where(c => c.Fecha.Date == fecha.Date)
                .Select(c => new CotizacionView
                {
                    MonedaCodigo = c.Moneda.Codigo,
                    Fecha = c.Fecha,
                    CotizacionCompra = c.CotizacionCompra,
                    CotizacionVenta = c.CotizacionVenta
                })
                .ToList();
            
            dgCotizaciones.ItemsSource = cotizaciones;
        }

        private decimal ParsearMonto(string? texto)
        {
            return MontoHelper.Parsear(texto);
        }

        private void BtnGuardarCotizacion_Click(object? sender, RoutedEventArgs e)
        {
            var itemMoneda = cmbMonedaCotiz.SelectedItem as ComboBoxItem;
            if (itemMoneda?.Tag is not int monedaId) return;
            
            decimal cotizCompra = ParsearMonto(txtCotizCompra.Text);
            if (cotizCompra <= 0) return;
            
            DateTime fecha = dpFechaCotizacion.SelectedDate?.Date ?? DateTime.Today;
            
            using var db = _contextFactory.CreateDbContext();
            
            var existente = db.CotizacionesDiarias
                .FirstOrDefault(c => c.MonedaId == monedaId && c.Fecha.Date == fecha.Date);
            
            if (existente != null)
            {
                existente.CotizacionCompra = cotizCompra;
                existente.CotizacionVenta = cotizCompra * 1.02m;
            }
            else
            {
                var nueva = new CotizacionDiaria
                {
                    MonedaId = monedaId,
                    Fecha = fecha,
                    CotizacionCompra = cotizCompra,
                    CotizacionVenta = cotizCompra * 1.02m
                };
                db.CotizacionesDiarias.Add(nueva);
            }
            
            db.SaveChanges();
            BtnCargarCotizaciones_Click(sender, e);
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async System.Threading.Tasks.Task MostrarMensaje(string titulo, string mensaje)
        {
            var dialog = new Window
            {
                Title = titulo,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#161b22"))
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
            panel.Children.Add(new TextBlock 
            { 
                Text = mensaje, 
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e6edf3"))
            });

            var btnOk = new Button 
            { 
                Content = "OK", 
                Width = 100, 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#238636")),
                Foreground = Avalonia.Media.Brushes.White
            };
            btnOk.Click += (s, ev) => dialog.Close();

            panel.Children.Add(btnOk);
            dialog.Content = panel;

            await dialog.ShowDialog(this);
        }
    }

    public class CotizacionView
    {
        public string MonedaCodigo { get; set; } = "";
        public DateTime Fecha { get; set; }
        public decimal CotizacionCompra { get; set; }
        public decimal CotizacionVenta { get; set; }
    }
}
