using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SistemaCambio.Models;
using System;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class CreditoDebitoWindow : Window
    {
        public CreditoDebitoWindow()
        {
            InitializeComponent();
            CargarDatos();
        }

        private void CargarDatos()
        {
            using var db = new AppDbContext();

            // Cargar cuentas
            var cuentas = db.Cuentas.OrderBy(c => c.Nombre).ToList();
            foreach (var cuenta in cuentas)
            {
                cmbCredito.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre}", Tag = cuenta.Id });
                cmbDebito.Items.Add(new ComboBoxItem { Content = $"{cuenta.Nombre}", Tag = cuenta.Id });
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
            return Services.MontoHelper.Parsear(texto);
        }

        private void Recalcular_KeyUp(object? sender, KeyEventArgs e)
        {
            // Copiar importe crédito a débito por defecto
            txtImporteDebito.Text = txtImporteCredito.Text;
        }

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is TextBox textBox) textBox.SelectAll();
        }

        private void BtnEjecutar_Click(object? sender, RoutedEventArgs e)
        {
            EjecutarOperacion();
        }

        private void BtnAceptar_Click(object? sender, RoutedEventArgs e)
        {
            EjecutarOperacion();
            Close();
        }

        private void EjecutarOperacion()
        {
            decimal importeCredito = ParsearMonto(txtImporteCredito.Text);
            decimal importeDebito = ParsearMonto(txtImporteDebito.Text);

            if (importeCredito <= 0 && importeDebito <= 0) return;

            var itemCredito = cmbCredito.SelectedItem as ComboBoxItem;
            var itemDebito = cmbDebito.SelectedItem as ComboBoxItem;

            if (itemCredito?.Tag is not int creditoId || itemDebito?.Tag is not int debitoId) return;

            // Obtener cliente si está seleccionado
            int? clienteId = null;
            var itemCliente = cmbCliente.SelectedItem as ComboBoxItem;
            if (itemCliente?.Tag is int cId)
            {
                clienteId = cId;
            }

            using var db = new AppDbContext();

            var cuentaCredito = db.Cuentas.Find(creditoId);
            var cuentaDebito = db.Cuentas.Find(debitoId);

            if (cuentaCredito == null || cuentaDebito == null) return;

            // Crear operación
            var operacion = new Operacion
            {
                Fecha = DateTime.Now,
                TipoOperacion = "Credito/Debito",
                MontoTotalOrigen = importeDebito,
                MontoTotalDestino = importeCredito,
                CotizacionAplicada = ParsearMonto(txtCotizacion.Text),
                Observaciones = txtObservaciones.Text ?? "",
                ClienteId = clienteId
            };
            db.Operaciones.Add(operacion);

            // Movimiento: crédito (entra dinero)
            if (importeCredito > 0)
            {
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = creditoId,
                    Monto = importeCredito,
                    Fecha = DateTime.Now
                });
                cuentaCredito.Saldo += importeCredito;
            }

            // Movimiento: débito (sale dinero)
            if (importeDebito > 0)
            {
                db.Movimientos.Add(new Movimiento
                {
                    Operacion = operacion,
                    CuentaId = debitoId,
                    Monto = -importeDebito,
                    Fecha = DateTime.Now
                });
                cuentaDebito.Saldo -= importeDebito;
            }

            db.SaveChanges();
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
