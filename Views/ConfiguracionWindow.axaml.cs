using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class ConfiguracionWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;

        public ConfiguracionWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            dpFechaCotizacion.SelectedDate = new DateTimeOffset(DateTime.Today);
            CargarMonedasAsync();
        }

        private void ToggleTema_Changed(object? sender, RoutedEventArgs e)
        {
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = toggleTema.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private async void CargarMonedasAsync()
        {
            try
            {
                var monedas = await _apiClient.ObtenerMonedasAsync();
                dgMonedas.ItemsSource = monedas;
                CargarMonedaCombo(monedas);
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private void CargarMonedaCombo(List<MonedaDto> monedas)
        {
            cmbMonedaCotiz.Items.Clear();
            foreach (var moneda in monedas)
                cmbMonedaCotiz.Items.Add(new ComboBoxItem { Content = moneda.Codigo, Tag = moneda.Codigo });
            if (cmbMonedaCotiz.Items.Count > 0) cmbMonedaCotiz.SelectedIndex = 0;
        }

        private async void BtnNuevaMoneda_Click(object? sender, RoutedEventArgs e)
        {
            var codigo = txtNuevoCodigo.Text?.Trim().ToUpper();
            var nombre = txtNuevoNombre.Text?.Trim();
            if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(nombre))
            {
                await MostrarMensaje("Error", "Debe ingresar el codigo y el nombre de la moneda.");
                return;
            }
            try
            {
                await _apiClient.CrearMonedaAsync(new CrearMonedaRequest { Codigo = codigo, Nombre = nombre });
                txtNuevoCodigo.Text = "";
                txtNuevoNombre.Text = "";
                CargarMonedasAsync();
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private async void BtnGuardarCambios_Click(object? sender, RoutedEventArgs e)
        {
            await MostrarMensaje("Info", "Las monedas se gestionan individualmente desde el servidor.");
        }

        private void BtnRefrescarMonedas_Click(object? sender, RoutedEventArgs e) => CargarMonedasAsync();

        private async void BtnEliminarMoneda_Click(object? sender, RoutedEventArgs e)
        {
            await MostrarMensaje("Info", "La eliminacion de monedas se gestiona desde el servidor.");
        }

        private async void BtnCargarCotizaciones_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var cotizaciones = await _apiClient.ObtenerCotizacionesHoyAsync();
                dgCotizaciones.ItemsSource = cotizaciones.Select(c => new CotizacionView
                {
                    MonedaCodigo = c.CodigoMoneda,
                    Fecha = c.Fecha,
                    CotizacionCompra = c.CotizacionCompra,
                    CotizacionVenta = c.CotizacionVenta
                }).ToList();
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private decimal ParsearMonto(string? texto) => MontoHelper.Parsear(texto);

        private async void BtnGuardarCotizacion_Click(object? sender, RoutedEventArgs e)
        {
            var itemMoneda = cmbMonedaCotiz.SelectedItem as ComboBoxItem;
            if (itemMoneda?.Tag is not string codigoMoneda) return;
            decimal cotizCompra = ParsearMonto(txtCotizCompra.Text);
            if (cotizCompra <= 0) return;

            try
            {
                await _apiClient.GuardarCotizacionAsync(new CrearCotizacionRequest
                {
                    CodigoMoneda = codigoMoneda,
                    CotizacionCompra = cotizCompra,
                    CotizacionVenta = cotizCompra * 1.02m
                });
                BtnCargarCotizaciones_Click(sender, e);
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();

        private async System.Threading.Tasks.Task MostrarMensaje(string titulo, string mensaje)
        {
            var dialog = new Window { Title = titulo, Width = 400, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#161b22")) };
            var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
            panel.Children.Add(new TextBlock { Text = mensaje, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e6edf3")) });
            var btnOk = new Button { Content = "OK", Width = 100, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#238636")), Foreground = Avalonia.Media.Brushes.White };
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
