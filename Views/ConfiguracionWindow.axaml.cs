using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class ConfiguracionWindow : Window
    {
        public ConfiguracionWindow()
        {
            InitializeComponent();
            dpFechaCotizacion.SelectedDate = new DateTimeOffset(DateTime.Today);
            CargarMonedas();
            CargarMonedaCombo();
        }

        private void ToggleTema_Changed(object? sender, RoutedEventArgs e)
        {
            if (Application.Current != null)
            {
                // IsChecked = true significa tema oscuro
                Application.Current.RequestedThemeVariant = toggleTema.IsChecked == true 
                    ? ThemeVariant.Dark 
                    : ThemeVariant.Light;
            }
        }

        private void CargarMonedas()
        {
            using var db = new AppDbContext();
            var monedas = db.Monedas.OrderBy(m => m.Codigo).ToList();
            dgMonedas.ItemsSource = monedas;
        }

        private void CargarMonedaCombo()
        {
            using var db = new AppDbContext();
            var monedas = db.Monedas.Where(m => m.Activa).ToList();
            
            cmbMonedaCotiz.Items.Clear();
            foreach (var moneda in monedas)
            {
                cmbMonedaCotiz.Items.Add(new ComboBoxItem { Content = moneda.Codigo, Tag = moneda.Id });
            }
            
            if (cmbMonedaCotiz.Items.Count > 0)
                cmbMonedaCotiz.SelectedIndex = 0;
        }

        private void BtnNuevaMoneda_Click(object? sender, RoutedEventArgs e)
        {
            // TODO: Abrir diálogo para nueva moneda
            using var db = new AppDbContext();
            
            // Por ahora, agregar una moneda de ejemplo
            var nuevaMoneda = new Moneda
            {
                Codigo = $"NEW{db.Monedas.Count() + 1}",
                Nombre = "Nueva Moneda",
                Activa = true
            };
            db.Monedas.Add(nuevaMoneda);
            db.SaveChanges();
            
            CargarMonedas();
            CargarMonedaCombo();
        }

        private void BtnRefrescarMonedas_Click(object? sender, RoutedEventArgs e)
        {
            CargarMonedas();
        }

        private void BtnCargarCotizaciones_Click(object? sender, RoutedEventArgs e)
        {
            using var db = new AppDbContext();
            
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
            return Services.MontoHelper.Parsear(texto);
        }

        private void BtnGuardarCotizacion_Click(object? sender, RoutedEventArgs e)
        {
            var itemMoneda = cmbMonedaCotiz.SelectedItem as ComboBoxItem;
            if (itemMoneda?.Tag is not int monedaId) return;
            
            decimal cotizCompra = ParsearMonto(txtCotizCompra.Text);
            if (cotizCompra <= 0) return;
            
            DateTime fecha = dpFechaCotizacion.SelectedDate?.Date ?? DateTime.Today;
            
            using var db = new AppDbContext();
            
            // Buscar si ya existe una cotización para esta moneda y fecha
            var existente = db.CotizacionesDiarias
                .FirstOrDefault(c => c.MonedaId == monedaId && c.Fecha.Date == fecha.Date);
            
            if (existente != null)
            {
                existente.CotizacionCompra = cotizCompra;
                existente.CotizacionVenta = cotizCompra * 1.02m; // 2% spread por defecto
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
    }

    // Clase auxiliar para mostrar cotizaciones
    public class CotizacionView
    {
        public string MonedaCodigo { get; set; } = "";
        public DateTime Fecha { get; set; }
        public decimal CotizacionCompra { get; set; }
        public decimal CotizacionVenta { get; set; }
    }
}
