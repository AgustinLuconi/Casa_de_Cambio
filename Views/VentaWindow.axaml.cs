using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using SistemaCambio.Models;
using System;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class VentaWindow : Window
    {
        public VentaWindow()
        {
            InitializeComponent();
            CargarDatos();
        }

        private void CargarDatos()
        {
            using var db = new AppDbContext();

            // Cargar monedas
            var monedas = db.Monedas.Where(m => m.Activa).ToList();
            if (monedas.Any())
            {
                foreach (var moneda in monedas)
                {
                    cmbMoneda.Items.Add(new ComboBoxItem { Content = $"{moneda.Codigo} - {moneda.Nombre}", Tag = moneda.Id });
                }
            }
            else
            {
                // Si no hay monedas, usar valores por defecto
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "USD - Dólar", Tag = "USD" });
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "EUR - Euro", Tag = "EUR" });
            }
            cmbMoneda.SelectedIndex = 0;

            // Cargar cuentas
            var cuentas = db.Cuentas.OrderBy(c => c.Nombre).ToList();
            foreach (var cuenta in cuentas)
            {
                var itemDebitar = new ComboBoxItem { Content = $"{cuenta.Nombre}", Tag = cuenta.Id };
                var itemAcreditar = new ComboBoxItem { Content = $"{cuenta.Nombre}", Tag = cuenta.Id };
                cmbDebitar.Items.Add(itemDebitar);
                cmbAcreditar.Items.Add(itemAcreditar);
            }

            if (cmbDebitar.Items.Count > 0) cmbDebitar.SelectedIndex = 0;
            if (cmbAcreditar.Items.Count > 0) cmbAcreditar.SelectedIndex = 0;

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
                        txtCotizacion.Text = cotizacion.CotizacionVenta.ToString("N5");
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
            decimal montoExtranjera = ParsearMonto(txtMontoExtranjera.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            decimal pesos = montoExtranjera * cotizacion;
            txtPesos.Text = pesos.ToString("N2");
        }

        private void CalcularVuelto_KeyUp(object? sender, KeyEventArgs e)
        {
            decimal pesos = ParsearMonto(txtPesos.Text);
            decimal ingresa = ParsearMonto(txtIngresa.Text);
            decimal vuelto = ingresa - pesos;
            txtVuelto.Text = vuelto.ToString("N2");
        }

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is TextBox textBox) textBox.SelectAll();
        }

        private async void BtnAceptar_Click(object? sender, RoutedEventArgs e)
        {
            decimal montoExtranjera = ParsearMonto(txtMontoExtranjera.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            decimal pesos = ParsearMonto(txtPesos.Text);

            if (montoExtranjera <= 0 || cotizacion <= 0) return;

            var itemDebitar = cmbDebitar.SelectedItem as ComboBoxItem;
            var itemAcreditar = cmbAcreditar.SelectedItem as ComboBoxItem;

            if (itemDebitar?.Tag is not int debitarId || itemAcreditar?.Tag is not int acreditarId) return;

            // Obtener moneda para validación PPP
            using var db = new AppDbContext();
            var cuentaDebitar = db.Cuentas.Find(debitarId);
            if (cuentaDebitar != null && cuentaDebitar.Moneda != "ARS")
            {
                // Validar contra PPP
                var pppValidacion = Services.PPPService.ValidarVenta(cuentaDebitar.Moneda, cotizacion);
                if (!string.IsNullOrEmpty(pppValidacion.Mensaje) && pppValidacion.Mensaje.StartsWith("⚠️"))
                {
                    // Mostrar advertencia pero permitir continuar
                    var msgBox = new Window
                    {
                        Title = "Alerta de Rentabilidad",
                        Width = 450,
                        Height = 180,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
                    panel.Children.Add(new TextBlock { Text = pppValidacion.Mensaje, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
                    var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, Margin = new Avalonia.Thickness(0, 15, 0, 0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                    var btnContinuar = new Button { Content = "Continuar de todas formas" };
                    var btnCancelar = new Button { Content = "Cancelar" };
                    bool continuar = false;
                    btnContinuar.Click += (s, ev) => { continuar = true; msgBox.Close(); };
                    btnCancelar.Click += (s, ev) => msgBox.Close();
                    btnPanel.Children.Add(btnContinuar);
                    btnPanel.Children.Add(btnCancelar);
                    panel.Children.Add(btnPanel);
                    msgBox.Content = panel;
                    await msgBox.ShowDialog(this);
                    if (!continuar) return;
                }
            }

            // Usar el servicio con transacciones atómicas
            var resultado = Services.OperacionService.GuardarOperacion(
                tipo: "Venta",
                cuentaOrigenId: debitarId,    // Sale divisa
                cuentaDestinoId: acreditarId, // Entran pesos
                montoOrigen: montoExtranjera,
                montoDestino: pesos,
                cotizacion: cotizacion,
                observaciones: txtObservaciones.Text ?? ""
            );

            if (!resultado.Exitoso)
            {
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

            // Registrar salida en PPP
            if (cuentaDebitar != null && cuentaDebitar.Moneda != "ARS")
            {
                Services.PPPService.RegistrarVenta(cuentaDebitar.Moneda, montoExtranjera);
            }

            Close();
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
