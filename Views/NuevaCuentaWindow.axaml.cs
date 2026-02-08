using Avalonia.Controls;
using Avalonia.Interactivity;
using SistemaCambio.Models;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class NuevaCuentaWindow : Window
    {
        public NuevaCuentaWindow()
        {
            InitializeComponent();
            CargarMonedas();
        }

        private void CargarMonedas()
        {
            using var db = new AppDbContext();
            
            var monedas = db.Monedas.Where(m => m.Activa).ToList();
            
            if (monedas.Any())
            {
                foreach (var moneda in monedas)
                {
                    cmbMoneda.Items.Add(new ComboBoxItem { Content = $"{moneda.Codigo} - {moneda.Nombre}", Tag = moneda.Codigo });
                }
            }
            else
            {
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "USD - Dólar", Tag = "USD" });
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "EUR - Euro", Tag = "EUR" });
                cmbMoneda.Items.Add(new ComboBoxItem { Content = "ARS - Peso Argentino", Tag = "ARS" });
            }
            
            cmbMoneda.SelectedIndex = 0;
        }

        private decimal ParsearMonto(string? texto)
        {
            return Services.MontoHelper.Parsear(texto);
        }

        private void BtnGuardar_Click(object? sender, RoutedEventArgs e)
        {
            string nombre = txtNombre.Text?.Trim() ?? "";
            
            if (string.IsNullOrEmpty(nombre))
            {
                return; // TODO: mostrar mensaje de error
            }

            var itemTipo = cmbTipo.SelectedItem as ComboBoxItem;
            var itemMoneda = cmbMoneda.SelectedItem as ComboBoxItem;

            string tipo = itemTipo?.Content?.ToString() ?? "Caja";
            string moneda = itemMoneda?.Tag?.ToString() ?? "USD";
            decimal saldoInicial = ParsearMonto(txtSaldoInicial.Text);

            using var db = new AppDbContext();
            
            var cuenta = new Cuenta
            {
                Nombre = nombre,
                Tipo = tipo,
                Moneda = moneda,
                Saldo = saldoInicial
            };
            
            db.Cuentas.Add(cuenta);
            db.SaveChanges();
            
            Close();
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
