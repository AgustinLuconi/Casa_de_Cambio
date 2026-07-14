using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Material.Icons;
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
        }

        private bool _isDarkMode = true;

        private void ToggleTema_Click(object? sender, PointerPressedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = _isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;

            if (_isDarkMode)
            {
                toggleTemaKnob.HorizontalAlignment = HorizontalAlignment.Left;
                toggleTemaKnob.Margin = new Thickness(3, 0, 0, 0);
                iconTema.Kind = MaterialIconKind.WeatherNight;
                iconTema.Foreground = Avalonia.Media.Brush.Parse("#1a6fa8");
            }
            else
            {
                toggleTemaKnob.HorizontalAlignment = HorizontalAlignment.Right;
                toggleTemaKnob.Margin = new Thickness(0, 0, 3, 0);
                iconTema.Kind = MaterialIconKind.WeatherSunny;
                iconTema.Foreground = Avalonia.Media.Brush.Parse("#d97706");
            }
        }

        private async void CargarMonedasAsync()
        {
            try
            {
                var monedas = await _apiClient.ObtenerMonedasAsync();
                dgMonedas.ItemsSource = monedas;
                CargarMonedaCombo(monedas);
                await CargarLimitesDivisaAsync(monedas);
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
            var tipoPase = (cmbNuevoTipoPase.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "D";
            if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(nombre))
            {
                await DialogHelper.MensajeAsync(this,"Error", "Debe ingresar el codigo y el nombre de la moneda.");
                return;
            }
            try
            {
                await _apiClient.CrearMonedaAsync(new CrearMonedaRequest { Codigo = codigo, Nombre = nombre, TipoPase = tipoPase });
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
                    await _apiClient.ActualizarMonedaAsync(m.Id, new ActualizarMonedaRequest { Codigo = m.Codigo, Nombre = m.Nombre, Activa = m.Activa, TipoPase = m.TipoPase });
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

        // ── Límites de deuda generales por divisa ────────────────────
        // Clave de configuración por moneda: limite_deuda_general_{CODIGO}

        private readonly System.Collections.ObjectModel.ObservableCollection<LimiteDivisaModel> _limitesDivisa = new();

        private async System.Threading.Tasks.Task CargarLimitesDivisaAsync(List<MonedaDto> monedas)
        {
            try
            {
                _limitesDivisa.Clear();
                // Una consulta por divisa, lanzadas en paralelo
                var tareas = monedas.OrderBy(m => m.Codigo).Select(async m =>
                {
                    var valor = await _apiClient.ObtenerConfiguracionAsync($"limite_deuda_general_{m.Codigo}");
                    return new LimiteDivisaModel
                    {
                        Codigo = m.Codigo,
                        Nombre = m.Nombre,
                        LimiteTexto = valor ?? "0"
                    };
                }).ToList();

                foreach (var item in await System.Threading.Tasks.Task.WhenAll(tareas))
                    _limitesDivisa.Add(item);

                icLimitesDivisa.ItemsSource = _limitesDivisa;

                // Auto-marcar el toggle si TODAS las divisas tienen límite 0
                var todasEnCero = _limitesDivisa.All(m => MontoHelper.Parsear(m.LimiteTexto) == 0);
                toggleSinLimite.IsChecked = todasEnCero;
                icLimitesDivisa.IsEnabled = !todasEnCero;
            }
            catch (Exception ex) { AppLogger.Warn("CargarLimitesDivisaAsync", ex); }
        }

        private void ToggleSinLimite_Changed(object? sender, RoutedEventArgs e)
        {
            var sinLimite = toggleSinLimite.IsChecked == true;
            icLimitesDivisa.IsEnabled = !sinLimite;

            if (sinLimite)
            {
                // Mostrar 0 en todos los campos visualmente, sin guardar aún
                foreach (var item in _limitesDivisa)
                    item.LimiteTexto = "0";

                // Refrescar el ItemsControl para que los TextBox muestren el nuevo valor
                icLimitesDivisa.ItemsSource = null;
                icLimitesDivisa.ItemsSource = _limitesDivisa;
            }
        }

        private async void BtnGuardarLimitesDivisa_Click(object? sender, RoutedEventArgs e)
        {
            var errores = new List<string>();
            int guardados = 0;

            // Si el toggle está activo, forzar 0 en todos
            var sinLimite = toggleSinLimite.IsChecked == true;

            foreach (var item in _limitesDivisa)
            {
                decimal limite = sinLimite ? 0 : MontoHelper.Parsear(item.LimiteTexto);
                if (!sinLimite && limite < 0)
                {
                    errores.Add($"{item.Codigo}: el límite no puede ser negativo.");
                    continue;
                }
                var ok = await _apiClient.ActualizarConfiguracionAsync(
                    $"limite_deuda_general_{item.Codigo}",
                    limite.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (ok) guardados++;
                else errores.Add($"{item.Codigo}: no se pudo guardar.");
            }

            if (errores.Any())
                await DialogHelper.MensajeAsync(this, "Error", string.Join("\n", errores));
            else
            {
                var msg = sinLimite
                    ? $"Límites desactivados para todas las divisas ({guardados} divisa(s))."
                    : $"Límites de deuda actualizados para {guardados} divisa(s).";
                await DialogHelper.MensajeAsync(this, "Éxito", msg);
            }
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

    /// <summary>Fila editable del límite de deuda global para una divisa.</summary>
    public class LimiteDivisaModel
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string LimiteTexto { get; set; } = "0";
    }
}
