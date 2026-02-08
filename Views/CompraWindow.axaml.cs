using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SistemaCambio.Models;
using System;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class CompraWindow : Window
    {
        public CompraWindow()
        {
            InitializeComponent();
            CargarDatos();
        }

        private void CargarDatos()
        {
            using var db = new AppDbContext();

            // Cargar monedas dinámicamente
            var monedas = db.Monedas.Where(m => m.Activa).ToList();
            cmbMoneda.Items.Clear();
            
            if (monedas.Any())
            {
                foreach (var moneda in monedas)
                {
                    cmbMoneda.Items.Add(new ComboBoxItem { Content = $"{moneda.Codigo} - {moneda.Nombre}", Tag = moneda.Id });
                }
            }
            else
            {
                // Valores por defecto si no hay monedas
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "USD - Dólar", Tag = "USD" });
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "EUR - Euro", Tag = "EUR" });
            }
            cmbMoneda.SelectedIndex = 0;

            // Cargar cuentas dinámicamente
            var cuentas = db.Cuentas.OrderBy(c => c.Nombre).ToList();
            
            cmbDestino.Items.Clear();
            cmbOrigen.Items.Clear();
            
            foreach (var cuenta in cuentas)
            {
                cmbDestino.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = cuenta.Id });
                cmbOrigen.Items.Add(new ComboBoxItem { Content = cuenta.Nombre, Tag = cuenta.Id });
            }

            if (cmbDestino.Items.Count > 0) cmbDestino.SelectedIndex = 0;
            if (cmbOrigen.Items.Count > 0) cmbOrigen.SelectedIndex = 0;

            // Cargar cotización del día
            CargarCotizacionDelDia();
        }

        private void CmbMoneda_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            CargarCotizacionDelDia();
        }

        private void CargarCotizacionDelDia()
        {
            try
            {
                using var db = new AppDbContext();
                var selectedItem = cmbMoneda.SelectedItem as ComboBoxItem;
                if (selectedItem?.Tag is int monedaId)
                {
                    var cotizacion = db.CotizacionesDiarias
                        .Where(c => c.MonedaId == monedaId && c.Fecha.Date == DateTime.Today)
                        .FirstOrDefault();

                    if (cotizacion != null)
                    {
                        txtCotizacion.Text = cotizacion.CotizacionCompra.ToString("N5");
                    }
                }
            }
            catch
            {
                // Si falla, mantener el valor actual
            }
        }

        private decimal ParsearMonto(string? texto)
        {
            return Services.MontoHelper.Parsear(texto);
        }

        private void Recalcular_KeyUp(object? sender, KeyEventArgs e)
        {
            decimal montoDestino = ParsearMonto(txtMontoDestino.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            decimal montoOrigen = montoDestino * cotizacion;
            txtMontoOrigen.Text = montoOrigen.ToString("N2");
            
            // Calcular vuelto
            CalcularVuelto();
        }

        private void CalcularVuelto()
        {
            decimal montoOrigen = ParsearMonto(txtMontoOrigen.Text);
            decimal pagaCon = ParsearMonto(txtPagaCon.Text);
            decimal vuelto = pagaCon - montoOrigen;
            txtVuelto.Text = vuelto.ToString("N2");
        }

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        private async void BotonAceptar_Click(object? sender, RoutedEventArgs e)
        {
            decimal montoDestino = ParsearMonto(txtMontoDestino.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            decimal montoOrigen = ParsearMonto(txtMontoOrigen.Text);

            if (montoDestino <= 0 || cotizacion <= 0)
                return;

            var itemOrigen = cmbOrigen.SelectedItem as ComboBoxItem;
            var itemDestino = cmbDestino.SelectedItem as ComboBoxItem;

            if (itemOrigen?.Tag is not int origenId || itemDestino?.Tag is not int destinoId)
                return;

            // Usar el servicio con transacciones atómicas
            var resultado = Services.OperacionService.GuardarOperacion(
                tipo: "Compra",
                cuentaOrigenId: origenId,
                cuentaDestinoId: destinoId,
                montoOrigen: montoOrigen,
                montoDestino: montoDestino,
                cotizacion: cotizacion,
                observaciones: "Compra de divisa"
            );

            if (!resultado.Exitoso)
            {
                // Mostrar error al usuario
                var msgBox = new Window
                {
                    Title = "Error",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
                panel.Children.Add(new TextBlock { Text = resultado.Mensaje, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
                panel.Children.Add(new Button { Content = "OK", Margin = new Avalonia.Thickness(0, 15, 0, 0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
                ((Button)panel.Children[1]).Click += (s, ev) => msgBox.Close();
                msgBox.Content = panel;
                await msgBox.ShowDialog(this);
                return;
            }

            // Registrar en PPP (compra de divisa = ingresa a inventario)
            using var db = new AppDbContext();
            var cuentaDestino = db.Cuentas.Find(destinoId);
            if (cuentaDestino != null && cuentaDestino.Moneda != "ARS")
            {
                Services.PPPService.RegistrarCompra(cuentaDestino.Moneda, montoDestino, montoOrigen);
            }

            Close();
        }

        private void BotonCancelar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}