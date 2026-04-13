using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using SistemaCambio.Services.Offline;
using CasaCambio.Shared.Requests;
using System;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class CreditoDebitoWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly IOfflineOperacionService _offlineService;

        public CreditoDebitoWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            _offlineService = App.Services.GetRequiredService<IOfflineOperacionService>();

            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();
            CargarDatosAsync();
        }

        private async void CargarDatosAsync()
        {
            try
            {
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                cmbCredito.Items.Clear();
                cmbDebito.Items.Clear();
                foreach (var cuenta in cuentas.OrderBy(c => c.Nombre))
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

                var clientes = await _apiClient.ObtenerClientesAsync();
                cmbCliente.Items.Add(new ComboBoxItem { Content = "(Sin cliente)", Tag = null });
                foreach (var cliente in clientes)
                    cmbCliente.Items.Add(new ComboBoxItem { Content = cliente.Nombre, Tag = cliente.Id });
                cmbCliente.SelectedIndex = 0;
            }
            catch (Exception ex) { NotificationService.Error("Error al cargar datos", ex.Message); }
        }

        private static decimal ParsearMonto(string? texto) => MontoHelper.Parsear(texto);

        private void Recalcular_KeyUp(object? sender, KeyEventArgs e)
        {
            txtImporteDebito.Text = txtImporteCredito.Text;
        }

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
                NotificationService.Success("Credito/Debito registrado", "Operacion completada");
                Close();
            }
        }

        private bool ValidarCampos()
        {
            decimal importeCredito = ParsearMonto(txtImporteCredito.Text);
            decimal importeDebito = ParsearMonto(txtImporteDebito.Text);
            if (importeCredito <= 0 && importeDebito <= 0)
            {
                NotificationService.Warning("Campo requerido", "Ingrese al menos un importe mayor a cero.");
                txtImporteCredito.Focus();
                return false;
            }
            var itemCredito = cmbCredito.SelectedItem as ComboBoxItem;
            var itemDebito = cmbDebito.SelectedItem as ComboBoxItem;
            if (itemCredito?.Tag is not CuentaMonedaTag || itemDebito?.Tag is not CuentaMonedaTag)
            {
                NotificationService.Warning("Selección incompleta", "Seleccione las cuentas crédito y débito.");
                return false;
            }
            return true;
        }

        private void MostrarErrorServidor(string mensaje)
        {
            borderError.IsVisible = true;
            txtErrorServidor.Text = mensaje;
        }

        private void OcultarErrorServidor()
        {
            borderError.IsVisible = false;
            txtErrorServidor.Text = "";
        }

        private async System.Threading.Tasks.Task<bool> EjecutarOperacion()
        {
            OcultarErrorServidor();
            if (!ValidarCampos()) return false;

            decimal importeCredito = ParsearMonto(txtImporteCredito.Text);
            decimal importeDebito = ParsearMonto(txtImporteDebito.Text);

            var itemCredito = cmbCredito.SelectedItem as ComboBoxItem;
            var itemDebito = cmbDebito.SelectedItem as ComboBoxItem;
            var tagCredito = (CuentaMonedaTag)itemCredito!.Tag!;
            var tagDebito = (CuentaMonedaTag)itemDebito!.Tag!;

            int? clienteId = null;
            var itemCliente = cmbCliente.SelectedItem as ComboBoxItem;
            if (itemCliente?.Tag is int cId) clienteId = cId;

            var request = new CrearCreditoDebitoRequest
            {
                CuentaCreditoId = tagCredito.CuentaId,
                CuentaDebitoId = tagDebito.CuentaId,
                MonedaCredito = tagCredito.Moneda,
                MonedaDebito = tagDebito.Moneda,
                MontoCredito = importeCredito,
                MontoDebito = importeDebito,
                Cotizacion = ParsearMonto(txtCotizacion.Text),
                ClienteId = clienteId,
                Observaciones = txtObservaciones.Text ?? ""
            };

            var resultado = await _offlineService.GuardarCreditoDebitoAsync(request);
            if (!resultado.Exitoso)
            {
                MostrarErrorServidor(resultado.Mensaje);
                return false;
            }
            return true;
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
