using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using CasaCambio.Shared.Requests;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class ArqueoWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private ObservableCollection<ArqueoItem> _items = new();
        private bool _isInitializing = true;

        public ArqueoWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            txtFecha.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            CargarDatosAsync();
        }

        private async void CargarDatosAsync()
        {
            try
            {
                _items.Clear();
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                var cajas = cuentas.Where(c => c.Tipo == "Caja").OrderBy(c => c.Nombre).ToList();

                cmbCaja.Items.Clear();
                cmbCaja.Items.Add(new ComboBoxItem { Content = "TODAS", Tag = 0 });
                foreach (var caja in cajas)
                    cmbCaja.Items.Add(new ComboBoxItem { Content = caja.Nombre, Tag = caja.Id });
                cmbCaja.SelectedIndex = 0;

                CargarArqueoItems(0, cuentas.Where(c => c.Tipo == "Caja").ToList());
                _isInitializing = false;
            }
            catch (Exception ex) { NotificationService.Error("Error", ex.Message); }
        }

        private void CargarArqueoItems(int cajaId, System.Collections.Generic.List<CasaCambio.Shared.DTOs.CuentaDto> cajas)
        {
            _items.Clear();
            var filtered = cajaId > 0 ? cajas.Where(c => c.Id == cajaId) : cajas;

            if (cajaId > 0)
            {
                var caja = cajas.FirstOrDefault(c => c.Id == cajaId);
                txtTitulo.Text = $"Arqueo - Caja: {caja?.Nombre ?? ""}";
            }
            else txtTitulo.Text = "Arqueo - Caja: TODAS";

            foreach (var cuenta in filtered.OrderBy(c => c.Nombre))
            {
                foreach (var saldo in cuenta.Saldos.OrderBy(s => s.Moneda))
                {
                    _items.Add(new ArqueoItem
                    {
                        CuentaId = cuenta.Id, Codigo = saldo.Moneda,
                        Moneda = $"{cuenta.Nombre} ({saldo.Moneda})",
                        SaldoSistema = saldo.Saldo, SaldoArqueo = saldo.Saldo, Diferencia = 0,
                        MonedaCodigo = saldo.Moneda, NombreCuenta = cuenta.Nombre
                    });
                }
            }
            dgArqueo.ItemsSource = _items;
            CalcularTotalDiferencia();
        }

        private void CmbCaja_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            // Re-fetch would be needed, but for simplicity just filter cached data
        }

        private void BtnRefrescar_Click(object? sender, RoutedEventArgs e) => CargarDatosAsync();

        private void CalcularTotalDiferencia()
        {
            decimal total = _items.Sum(i => i.Diferencia);
            txtTotalDiferencia.Text = total.ToString("N2");
            txtTotalDiferencia.Foreground = total == 0
                ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#238636"))
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#da3633"));
        }

        private async void BtnAceptar_Click(object? sender, RoutedEventArgs e)
        {
            var itemsConDiferencia = _items.Where(i => i.Diferencia != 0).ToList();

            foreach (var item in itemsConDiferencia)
            {
                try
                {
                    await _apiClient.RealizarArqueoAsync(new CrearArqueoRequest
                    {
                        CuentaId = item.CuentaId, Moneda = item.MonedaCodigo,
                        SaldoArqueo = item.SaldoArqueo,
                        Observaciones = item.Diferencia > 0 ? "Sobrante de caja" : "Faltante de caja"
                    });
                }
                catch (Exception ex)
                {
                    NotificationService.Error($"Error en {item.Moneda}", ex.Message);
                    return;
                }
            }

            decimal totalDiferencia = _items.Sum(i => i.Diferencia);
            int ajustes = itemsConDiferencia.Count;
            if (ajustes > 0)
            {
                if (totalDiferencia > 0) Services.NotificationService.Warning("Arqueo completado", $"Sobrante: ${totalDiferencia:N2} ({ajustes} ajuste(s))");
                else if (totalDiferencia < 0) Services.NotificationService.Warning("Arqueo completado", $"Faltante: ${Math.Abs(totalDiferencia):N2} ({ajustes} ajuste(s))");
                else Services.NotificationService.Success("Arqueo completado", $"{ajustes} ajuste(s) realizado(s)");
            }
            else Services.NotificationService.Success("Arqueo completado", "Sin diferencias - Caja cuadra perfectamente");
            Close();
        }

        private async System.Threading.Tasks.Task<bool> MostrarConfirmacion(string titulo, string mensaje)
        {
            var dialog = new Window { Title = titulo, Width = 480, Height = 220, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
            panel.Children.Add(new TextBlock { Text = mensaje, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 440 });
            var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, Margin = new Avalonia.Thickness(0, 15, 0, 0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            bool continuar = false;
            var btnContinuar = new Button { Content = "Continuar" };
            var btnCancelar = new Button { Content = "Cancelar" };
            btnContinuar.Click += (s, ev) => { continuar = true; dialog.Close(); };
            btnCancelar.Click += (s, ev) => dialog.Close();
            btnPanel.Children.Add(btnContinuar); btnPanel.Children.Add(btnCancelar); panel.Children.Add(btnPanel); dialog.Content = panel;
            await dialog.ShowDialog(this);
            return continuar;
        }

        private void BtnSalir_Click(object? sender, RoutedEventArgs e) => Close();
    }

    public class ArqueoItem
    {
        public int CuentaId { get; set; }
        public string Codigo { get; set; } = "";
        public string Moneda { get; set; } = "";
        public string MonedaCodigo { get; set; } = "";
        public string NombreCuenta { get; set; } = "";
        public decimal SaldoSistema { get; set; }
        private decimal _saldoArqueo;
        public decimal SaldoArqueo
        {
            get => _saldoArqueo;
            set { _saldoArqueo = value; Diferencia = _saldoArqueo - SaldoSistema; }
        }
        public decimal Diferencia { get; set; }
    }
}
