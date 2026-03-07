using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;
using SistemaCambio.Services;
using SistemaCambio.Services.Validators;
using System;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class VentaWindow : Window
    {
        private readonly IOperacionService _operacionService;
        private readonly IPPPService _pppService;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly OperacionValidator _validator;

        public VentaWindow()
        {
            _operacionService = App.Services.GetRequiredService<IOperacionService>();
            _pppService = App.Services.GetRequiredService<IPPPService>();
            _contextFactory = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            _validator = App.Services.GetRequiredService<OperacionValidator>();

            InitializeComponent();
            CargarDatos();
        }

        private void CargarDatos()
        {
            using var db = _contextFactory.CreateDbContext();

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
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "USD - Dólar", Tag = "USD" });
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "EUR - Euro", Tag = "EUR" });
            }
            cmbMoneda.SelectedIndex = 0;

            // Cargar cuentas
            var cuentas = db.Cuentas
                .Include(c => c.Saldos)
                .OrderBy(c => c.Nombre)
                .ToList();

            cmbDebitar.Items.Clear();
            cmbAcreditar.Items.Clear();

            foreach (var cuenta in cuentas)
            {
                if (cuenta.Saldos.Any())
                {
                    foreach (var saldo in cuenta.Saldos.OrderBy(s => s.Moneda))
                    {
                        var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = saldo.Moneda, NombreCuenta = cuenta.Nombre };
                        var texto = $"{cuenta.Nombre} ({saldo.Moneda})";
                        cmbDebitar.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                        cmbAcreditar.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                    }
                }
                else
                {
                    var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = "ARS", NombreCuenta = cuenta.Nombre };
                    cmbDebitar.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                    cmbAcreditar.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                }
            }

            if (cmbDebitar.Items.Count > 0) cmbDebitar.SelectedIndex = 0;
            if (cmbAcreditar.Items.Count > 0) cmbAcreditar.SelectedIndex = 0;

            CargarCotizacionDelDia();
            ActualizarLabelsMoneda();
        }

        private void CmbMoneda_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            CargarCotizacionDelDia();
            ActualizarLabelsMoneda();
        }

        private string ObtenerCodigoMonedaSeleccionada()
        {
            if (cmbMoneda.SelectedItem is ComboBoxItem item)
            {
                var content = item.Content?.ToString() ?? "";
                if (content.Contains(" - "))
                {
                    return content.Split(" - ")[0].Trim();
                }
            }
            return "USD";
        }

        private void ActualizarLabelsMoneda()
        {
            // Removed to allow agnostic account choice rather than forced matching
        }

        private void CargarCotizacionDelDia()
        {
            try
            {
                using var db = _contextFactory.CreateDbContext();
                var selectedItem = cmbMoneda.SelectedItem as ComboBoxItem;
                if (selectedItem?.Tag is int monedaId)
                {
                    var cotizacion = db.CotizacionesDiarias
                        .Where(c => c.MonedaId == monedaId && c.Fecha.Date == DateTime.UtcNow.Date)
                        .FirstOrDefault();

                    if (cotizacion != null)
                    {
                        txtCotizacion.Text = cotizacion.CotizacionVenta.ToString("N5");
                    }
                }
            }
            catch
            {
            }
        }

        private decimal ParsearMonto(string? texto)
        {
            return MontoHelper.Parsear(texto);
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

            if (itemDebitar?.Tag is not CuentaMonedaTag tagDebitar ||
                itemAcreditar?.Tag is not CuentaMonedaTag tagAcreditar)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione las cuentas");
                return;
            }

            string monedaDebitar = tagDebitar.Moneda;
            string monedaAcreditar = tagAcreditar.Moneda;

            bool esInterbancaria = (cmbTipoOperacion.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Interbancaria";

            ValidationResult validacion;
            if (esInterbancaria)
            {
                validacion = _validator.ValidarOperacionInterbancaria(
                    tagDebitar.CuentaId, tagAcreditar.CuentaId,
                    monedaDebitar, monedaAcreditar,
                    montoExtranjera, pesos);
            }
            else
            {
                // ═══ VALIDACIÓN CENTRALIZADA ═══
                validacion = _validator.ValidarOperacion(
                    "Venta", tagDebitar.CuentaId, tagAcreditar.CuentaId,
                    monedaDebitar, monedaAcreditar,
                    montoExtranjera, pesos, cotizacion);
            }

            if (validacion.HasErrors)
            {
                NotificationService.Error("Errores de validación",
                    string.Join("\n", validacion.Errors.Select(err => $"• {err.Message}")));
                return;
            }

            if (validacion.HasWarnings)
            {
                var mensajeWarnings = string.Join("\n\n",
                    validacion.Warnings.Select(w => $"⚠️ {w.Message}\n   {w.Details}"));
                var continuar = await MostrarConfirmacion("Advertencias detectadas",
                    $"{mensajeWarnings}\n\n¿Desea continuar de todas formas?");
                if (!continuar) return;
            }

            // Validar cotización contra oficial
            if (monedaDebitar != "ARS")
            {
                var cotizValidacion = _validator.ValidarCotizacionContraOficial(
                    monedaDebitar, cotizacion);
                if (cotizValidacion.HasWarnings)
                {
                    var w = cotizValidacion.Warnings.First();
                    var continuar = await MostrarConfirmacion(
                        "Cotización inusual", $"{w.Message}\n{w.Details}\n\n¿Desea continuar?");
                    if (!continuar) return;
                }

                // Validar PPP
                var pppValidacion = _pppService.ValidarVenta(monedaDebitar, cotizacion);
                if (!string.IsNullOrEmpty(pppValidacion.Mensaje) && pppValidacion.Mensaje.StartsWith("⚠️"))
                {
                    var continuar = await MostrarConfirmacion(
                        "Alerta de Rentabilidad", $"{pppValidacion.Mensaje}\n\n¿Desea continuar?");
                    if (!continuar) return;
                }
            }

            OperacionResult resultado;
            if (esInterbancaria)
            {
                resultado = _operacionService.GuardarOperacionInterbancaria(
                    tipo: "Venta",
                    cuentaOrigenId: tagDebitar.CuentaId,
                    cuentaDestinoId: tagAcreditar.CuentaId,
                    monedaOrigen: monedaDebitar,
                    monedaDestino: monedaAcreditar,
                    montoOrigen: montoExtranjera,
                    montoDestino: pesos,
                    cotizacion: cotizacion,
                    observaciones: txtObservaciones.Text ?? ""
                );
            }
            else
            {
                resultado = _operacionService.GuardarOperacion(
                    tipo: "Venta",
                    cuentaOrigenId: tagDebitar.CuentaId,
                    cuentaDestinoId: tagAcreditar.CuentaId,
                    monedaOrigen: monedaDebitar,
                    monedaDestino: monedaAcreditar,
                    montoOrigen: montoExtranjera,
                    montoDestino: pesos,
                    cotizacion: cotizacion,
                    observaciones: txtObservaciones.Text ?? ""
                );
            }

            if (!resultado.Exitoso)
            {
                NotificationService.Error("Error al guardar venta", resultado.Mensaje);
                return;
            }

            if (monedaDebitar != "ARS")
                _pppService.RegistrarVenta(monedaDebitar, montoExtranjera);

            NotificationService.OperacionGuardada("Venta", resultado.OperacionId ?? 0);
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

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
