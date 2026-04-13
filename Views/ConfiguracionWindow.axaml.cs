using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Views.Helpers;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace SistemaCambio.Views
{
    public partial class ConfiguracionWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;

        public ConfiguracionWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();
            dpFechaCotizacion.SelectedDate = new DateTimeOffset(DateTime.Today);
            CargarMonedasAsync();
            CargarLimiteDeudaAsync();
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
            catch (Exception ex) { await DialogHelper.MensajeAsync(this,"Error", ex.Message); }
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
                await DialogHelper.MensajeAsync(this,"Error", "Debe ingresar el codigo y el nombre de la moneda.");
                return;
            }
            try
            {
                await _apiClient.CrearMonedaAsync(new CrearMonedaRequest { Codigo = codigo, Nombre = nombre });
                txtNuevoCodigo.Text = "";
                txtNuevoNombre.Text = "";
                CargarMonedasAsync();
            }
            catch (Exception ex) { await DialogHelper.MensajeAsync(this,"Error", ex.Message); }
        }

        private async void BtnGuardarCambios_Click(object? sender, RoutedEventArgs e)
        {
            if (dgMonedas.ItemsSource is not System.Collections.IEnumerable items) return;
            var monedas = items.OfType<MonedaDto>().ToList();
            var errores = new List<string>();
            foreach (var m in monedas)
            {
                try
                {
                    await _apiClient.ActualizarMonedaAsync(m.Id, new ActualizarMonedaRequest { Codigo = m.Codigo, Nombre = m.Nombre, Activa = m.Activa });
                }
                catch (HttpRequestException ex) { errores.Add($"{m.Codigo}: {ex.Message}"); }
            }
            if (errores.Any())
                await DialogHelper.MensajeAsync(this,"Error", string.Join("\n", errores));
            else
                await DialogHelper.MensajeAsync(this,"Éxito", $"{monedas.Count} moneda(s) actualizadas correctamente.");
            CargarMonedasAsync();
        }

        private void BtnRefrescarMonedas_Click(object? sender, RoutedEventArgs e) => CargarMonedasAsync();

        private async void BtnEliminarMoneda_Click(object? sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not MonedaDto moneda) return;

            var resultado = await DialogHelper.ConfirmarAsync(this,
                "Eliminar Moneda",
                $"¿Está seguro que desea eliminar la moneda \"{moneda.Codigo} - {moneda.Nombre}\"?\nEsta acción no se puede deshacer.",
                "Sí, eliminar", destructivo: true);
            if (!resultado) return;
            try
            {
                await _apiClient.EliminarMonedaAsync(moneda.Id);
                await DialogHelper.MensajeAsync(this,"Éxito", $"La moneda \"{moneda.Codigo}\" fue eliminada.");
                CargarMonedasAsync();
            }
            catch (HttpRequestException ex) { await DialogHelper.MensajeAsync(this,"Error", ex.Message); }
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
            catch (Exception ex) { await DialogHelper.MensajeAsync(this,"Error", ex.Message); }
        }

        private static decimal ParsearMonto(string? texto) => MontoHelper.Parsear(texto);

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
            catch (Exception ex) { await DialogHelper.MensajeAsync(this,"Error", ex.Message); }
        }

        private async void CargarLimiteDeudaAsync()
        {
            try
            {
                var valor = await _apiClient.ObtenerConfiguracionAsync("limite_deuda_general");
                if (valor != null) txtLimiteDeudaGeneral.Text = valor;
            }
            catch (Exception ex) { AppLogger.Warn("CargarLimiteDeudaAsync", ex); }
        }

        private async void BtnGuardarLimiteDeuda_Click(object? sender, RoutedEventArgs e)
        {
            var texto = txtLimiteDeudaGeneral.Text?.Trim() ?? "0";
            if (!decimal.TryParse(texto, out var limite) || limite < 0)
            {
                await DialogHelper.MensajeAsync(this,"Error", "Ingrese un valor numérico válido (0 o mayor).");
                return;
            }
            var ok = await _apiClient.ActualizarConfiguracionAsync("limite_deuda_general", limite.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (ok)
                await DialogHelper.MensajeAsync(this,"Éxito", $"Límite de deuda general actualizado a {limite:N2}.");
            else
                await DialogHelper.MensajeAsync(this,"Error", "No se pudo guardar la configuración.");
        }

        private void BtnCerrar_Click(object? sender, RoutedEventArgs e) => Close();

    }

    public class CotizacionView
    {
        public string MonedaCodigo { get; set; } = "";
        public DateTime Fecha { get; set; }
        public decimal CotizacionCompra { get; set; }
        public decimal CotizacionVenta { get; set; }
    }
}
