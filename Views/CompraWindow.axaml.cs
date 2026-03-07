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
    /// <summary>
    /// Clase auxiliar para almacenar CuentaId + Moneda juntos en el Tag del combo.
    /// </summary>
    public class CuentaMonedaTag
    {
        public int CuentaId { get; set; }
        public string Moneda { get; set; } = "";
        public string NombreCuenta { get; set; } = "";
    }

    public partial class CompraWindow : Window
    {
        private readonly IOperacionService _operacionService;
        private readonly IPPPService _pppService;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly OperacionValidator _validator;

        public CompraWindow()
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
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "USD - Dólar", Tag = "USD" });
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "EUR - Euro", Tag = "EUR" });
            }
            cmbMoneda.SelectedIndex = 0;

            // Cargar cuentas
            CargarComboCuentas(db);

            CargarCotizacionDelDia();
            ActualizarLabelsMoneda();
        }

        private void CargarComboCuentas(AppDbContext db)
        {
            var cuentas = db.Cuentas
                .Include(c => c.Saldos)
                .OrderBy(c => c.Nombre)
                .ToList();

            cmbDestino.Items.Clear();
            cmbOrigen.Items.Clear();

            foreach (var cuenta in cuentas)
            {
                if (cuenta.Saldos.Any())
                {
                    foreach (var saldo in cuenta.Saldos.OrderBy(s => s.Moneda))
                    {
                        var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = saldo.Moneda, NombreCuenta = cuenta.Nombre };
                        var texto = $"{cuenta.Nombre} ({saldo.Moneda})";
                        cmbDestino.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                        cmbOrigen.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                    }
                }
                else
                {
                    var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = "ARS", NombreCuenta = cuenta.Nombre };
                    cmbDestino.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                    cmbOrigen.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                }
            }

            if (cmbDestino.Items.Count > 0) cmbDestino.SelectedIndex = 0;
            if (cmbOrigen.Items.Count > 0) cmbOrigen.SelectedIndex = 0;
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
                        txtCotizacion.Text = cotizacion.CotizacionCompra.ToString("N5");
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
            decimal montoDestino = ParsearMonto(txtMontoDestino.Text);
            decimal cotizacion = ParsearMonto(txtCotizacion.Text);
            decimal montoOrigen = montoDestino * cotizacion;
            txtMontoOrigen.Text = montoOrigen.ToString("N2");
            
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

            if (montoDestino <= 0 || cotizacion <= 0) return;

            var itemOrigen = cmbOrigen.SelectedItem as ComboBoxItem;
            var itemDestino = cmbDestino.SelectedItem as ComboBoxItem;

            if (itemOrigen?.Tag is not CuentaMonedaTag tagOrigen ||
                itemDestino?.Tag is not CuentaMonedaTag tagDestino)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione las cuentas");
                return;
            }

            string monedaDestino = tagDestino.Moneda;
            string monedaOrigen = tagOrigen.Moneda;

            bool esInterbancaria = (cmbTipoOperacion.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Interbancaria";

            ValidationResult validacion;
            if (esInterbancaria)
            {
                validacion = _validator.ValidarOperacionInterbancaria(
                    tagOrigen.CuentaId, tagDestino.CuentaId,
                    monedaOrigen, monedaDestino,
                    montoOrigen, montoDestino);
            }
            else
            {
                // ═══ VALIDACIÓN CENTRALIZADA ═══
                validacion = _validator.ValidarOperacion(
                    "Compra", tagOrigen.CuentaId, tagDestino.CuentaId,
                    monedaOrigen, monedaDestino,
                    montoOrigen, montoDestino, cotizacion);
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

            OperacionResult resultado;
            if (esInterbancaria)
            {
                resultado = _operacionService.GuardarOperacionInterbancaria(
                    tipo: "Compra",
                    cuentaOrigenId: tagOrigen.CuentaId,
                    cuentaDestinoId: tagDestino.CuentaId,
                    monedaOrigen: monedaOrigen,
                    monedaDestino: monedaDestino,
                    montoOrigen: montoOrigen,
                    montoDestino: montoDestino,
                    cotizacion: cotizacion,
                    observaciones: txtObservaciones.Text ?? "Compra de divisa (Interbancaria)"
                );
            }
            else
            {
                resultado = _operacionService.GuardarOperacion(
                    tipo: "Compra",
                    cuentaOrigenId: tagOrigen.CuentaId,
                    cuentaDestinoId: tagDestino.CuentaId,
                    monedaOrigen: monedaOrigen,
                    monedaDestino: monedaDestino,
                    montoOrigen: montoOrigen,
                    montoDestino: montoDestino,
                    cotizacion: cotizacion,
                    observaciones: txtObservaciones.Text ?? "Compra de divisa"
                );
            }

            if (!resultado.Exitoso)
            {
                NotificationService.Error("Error al guardar compra", resultado.Mensaje);
                return;
            }

            // Registrar en PPP
            if (monedaDestino != "ARS")
                _pppService.RegistrarCompra(monedaDestino, montoDestino, montoOrigen);

            NotificationService.OperacionGuardada("Compra", resultado.OperacionId ?? 0);
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

        private void BotonCancelar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}