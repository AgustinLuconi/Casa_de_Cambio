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
    public partial class CreditoDebitoWindow : Window
    {
        private readonly IOperacionService _operacionService;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly OperacionValidator _validator;

        public CreditoDebitoWindow()
        {
            _operacionService = App.Services.GetRequiredService<IOperacionService>();
            _contextFactory = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            _validator = App.Services.GetRequiredService<OperacionValidator>();

            InitializeComponent();
            CargarDatos();
        }

        private void CargarDatos()
        {
            using var db = _contextFactory.CreateDbContext();

            // Cargar cuentas
            var cuentas = db.Cuentas
                .Include(c => c.Saldos)
                .OrderBy(c => c.Nombre)
                .ToList();

            cmbCredito.Items.Clear();
            cmbDebito.Items.Clear();

            foreach (var cuenta in cuentas)
            {
                if (cuenta.Saldos.Any())
                {
                    foreach (var saldo in cuenta.Saldos.OrderBy(s => s.Moneda))
                    {
                        var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = saldo.Moneda, NombreCuenta = cuenta.Nombre };
                        var texto = $"{cuenta.Nombre} ({saldo.Moneda})";
                        cmbCredito.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                        cmbDebito.Items.Add(new ComboBoxItem { Content = texto, Tag = tag });
                    }
                }
                else
                {
                    var tag = new CuentaMonedaTag { CuentaId = cuenta.Id, Moneda = "ARS", NombreCuenta = cuenta.Nombre };
                    cmbCredito.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                    cmbDebito.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre} (ARS)", Tag = tag });
                }
            }

            if (cmbCredito.Items.Count > 0) cmbCredito.SelectedIndex = 0;
            if (cmbDebito.Items.Count > 0) cmbDebito.SelectedIndex = 0;

            // Cargar clientes
            var clientes = db.Clientes.OrderBy(c => c.Nombre).ToList();
            cmbCliente.Items.Add(new ComboBoxItem { Content = "(Sin cliente)", Tag = null });
            foreach (var cliente in clientes)
            {
                cmbCliente.Items.Add(new ComboBoxItem { Content = cliente.Nombre, Tag = cliente.Id });
            }
            cmbCliente.SelectedIndex = 0;

            cmbCliente.SelectionChanged += (s, e) =>
            {
                var item = cmbCliente.SelectedItem as ComboBoxItem;
                if (item?.Tag is int clienteId)
                {
                    var cliente = clientes.FirstOrDefault(c => c.Id == clienteId);
                    txtDocumento.Text = cliente?.Documento ?? "";
                }
                else
                {
                    txtDocumento.Text = "";
                }
            };
        }

        private decimal ParsearMonto(string? texto)
        {
            return MontoHelper.Parsear(texto);
        }

        private void Recalcular_KeyUp(object? sender, KeyEventArgs e)
        {
            txtImporteDebito.Text = txtImporteCredito.Text;
        }

        // Method stubs removed as currency is now inferred from tag

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is TextBox textBox) textBox.SelectAll();
        }

        private async void BtnEjecutar_Click(object? sender, RoutedEventArgs e)
        {
            await EjecutarOperacion();
        }

        private async void BtnAceptar_Click(object? sender, RoutedEventArgs e)
        {
            if (await EjecutarOperacion())
            {
                NotificationService.Success("Crédito/Débito registrado", "Operación completada");
                Close();
            }
        }

        private async System.Threading.Tasks.Task<bool> EjecutarOperacion()
        {
            decimal importeCredito = ParsearMonto(txtImporteCredito.Text);
            decimal importeDebito = ParsearMonto(txtImporteDebito.Text);

            if (importeCredito <= 0 && importeDebito <= 0) return false;

            var itemCredito = cmbCredito.SelectedItem as ComboBoxItem;
            var itemDebito = cmbDebito.SelectedItem as ComboBoxItem;

            if (itemCredito?.Tag is not CuentaMonedaTag tagCredito ||
                itemDebito?.Tag is not CuentaMonedaTag tagDebito)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione las cuentas");
                return false;
            }

            string monedaCredito = tagCredito.Moneda;
            string monedaDebito = tagDebito.Moneda;

            // ═══ VALIDACIÓN CENTRALIZADA ═══
            var validacion = _validator.ValidarCreditoDebito(
                tagCredito.CuentaId, tagDebito.CuentaId,
                monedaCredito, monedaDebito,
                importeCredito, importeDebito);

            if (validacion.HasErrors)
            {
                NotificationService.Error("Errores de validación",
                    string.Join("\n", validacion.Errors.Select(err => $"• {err.Message}")));
                return false;
            }

            if (validacion.HasWarnings)
            {
                var mensajeWarnings = string.Join("\n\n",
                    validacion.Warnings.Select(w => $"⚠️ {w.Message}\n   {w.Details}"));
                var continuar = await MostrarConfirmacion("Advertencias detectadas",
                    $"{mensajeWarnings}\n\n¿Desea continuar de todas formas?");
                if (!continuar) return false;
            }

            int? clienteId = null;
            var itemCliente = cmbCliente.SelectedItem as ComboBoxItem;
            if (itemCliente?.Tag is int cId)
                clienteId = cId;

            // ═══ TODO VALIDADO — GUARDAR ═══
            var resultado = _operacionService.GuardarCreditoDebito(
                cuentaCreditoId: tagCredito.CuentaId,
                cuentaDebitoId: tagDebito.CuentaId,
                monedaCredito: monedaCredito,
                monedaDebito: monedaDebito,
                montoCredito: importeCredito,
                montoDebito: importeDebito,
                cotizacion: ParsearMonto(txtCotizacion.Text),
                clienteId: clienteId,
                observaciones: txtObservaciones.Text ?? ""
            );

            if (!resultado.Exitoso)
            {
                NotificationService.Error("Error en Crédito/Débito", resultado.Mensaje);
                return false;
            }

            return true;
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
