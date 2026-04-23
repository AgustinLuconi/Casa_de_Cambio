using Avalonia.Controls;
using Avalonia.Interactivity;
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
    public class SaldoInicialItem
    {
        public string Moneda { get; set; } = "";
        public string Nombre { get; set; } = "";
        public decimal Saldo { get; set; }
    }

    public partial class NuevaCuentaWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private List<SaldoInicialItem> _saldosIniciales = new();
        private int? _cuentaIdAEditar;

        public NuevaCuentaWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();
            cmbTipo.SelectionChanged += (s, e) =>
            {
                var tipo = (cmbTipo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                gridLimiteDeuda.IsVisible    = tipo == "Cliente";
                bool esEfectivo              = tipo == "Efectivo";
                gridMonedaEfectivo.IsVisible = esEfectivo;
                dgSaldosIniciales.IsVisible  = !esEfectivo;
            };
            CargarMonedasAsync();
            VerificarDiaCerradoAsync();
        }

        public NuevaCuentaWindow(int cuentaId) : this()
        {
            _cuentaIdAEditar = cuentaId;
            CargarDatosCuentaEdicionAsync();
        }

        private async void VerificarDiaCerradoAsync()
        {
            try
            {
                bool cerrado = await _apiClient.ObtenerEstadoDiaCerradoAsync();
                if (cerrado)
                {
                    btnGuardar.IsEnabled = false;
                    ToolTip.SetTip(btnGuardar, "El día está cerrado. Reabra la caja para modificar cuentas.");
                }
            }
            catch (Exception ex) { AppLogger.Warn("VerificarDiaCerradoAsync", ex); }
        }

        private async void CargarMonedasAsync()
        {
            try
            {
                var monedas = await _apiClient.ObtenerMonedasAsync();
                _saldosIniciales = monedas.Select(m => new SaldoInicialItem { Moneda = m.Codigo, Nombre = m.Nombre, Saldo = 0m }).ToList();
                if (!_saldosIniciales.Any())
                {
                    _saldosIniciales = new List<SaldoInicialItem>
                    {
                        new() { Moneda = "ARS", Nombre = "Peso Argentino" },
                        new() { Moneda = "USD", Nombre = "Dolar" },
                        new() { Moneda = "EUR", Nombre = "Euro" }
                    };
                }
                dgSaldosIniciales.ItemsSource = _saldosIniciales;

                cmbMonedaEfectivo.Items.Clear();
                foreach (var saldo in _saldosIniciales.OrderBy(s => s.Moneda))
                    cmbMonedaEfectivo.Items.Add(new ComboBoxItem
                    {
                        Content = $"{saldo.Moneda} — {saldo.Nombre}",
                        Tag     = saldo.Moneda
                    });
                if (cmbMonedaEfectivo.Items.Count > 0) cmbMonedaEfectivo.SelectedIndex = 0;
            }
            catch (Exception ex) { AppLogger.Warn("CargarMonedasAsync", ex); }
        }

        private async void CargarDatosCuentaEdicionAsync()
        {
            if (_cuentaIdAEditar == null) return;
            Title = "Editar Cuenta";
            txtTitulo.Text = "Editar Cuenta";
            txtSaldosTitulo.Text = "Saldos Actuales por Divisa";
            iconHeader.Kind = Material.Icons.MaterialIconKind.BankTransfer;

            try
            {
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                var cuenta = cuentas.FirstOrDefault(c => c.Id == _cuentaIdAEditar);
                if (cuenta != null)
                {
                    txtNombre.Text = cuenta.Nombre;
                    for (int i = 0; i < cmbTipo.Items.Count; i++)
                    {
                        if (cmbTipo.Items[i] is ComboBoxItem item && item.Content?.ToString() == cuenta.Tipo)
                        { cmbTipo.SelectedIndex = i; break; }
                    }
                    foreach (var saldoDB in cuenta.Saldos)
                    {
                        var saldoLocal = _saldosIniciales.FirstOrDefault(s => s.Moneda == saldoDB.Moneda);
                        if (saldoLocal != null) saldoLocal.Saldo = saldoDB.Saldo;
                        else _saldosIniciales.Add(new SaldoInicialItem { Moneda = saldoDB.Moneda, Nombre = saldoDB.Moneda, Saldo = saldoDB.Saldo });
                    }
                    dgSaldosIniciales.ItemsSource = null;
                    dgSaldosIniciales.ItemsSource = _saldosIniciales;

                    gridLimiteDeuda.IsVisible = cuenta.Tipo == "Cliente";
                    if (cuenta.Tipo == "Cliente" && cuenta.LimiteDeuda.HasValue && cuenta.LimiteDeuda.Value > 0)
                        txtLimiteDeuda.Text = cuenta.LimiteDeuda.Value.ToString("N2");

                    bool esEfectivo              = cuenta.Tipo == "Efectivo";
                    gridMonedaEfectivo.IsVisible = esEfectivo;
                    dgSaldosIniciales.IsVisible  = !esEfectivo;
                    if (esEfectivo)
                    {
                        var saldoUnico = cuenta.Saldos.FirstOrDefault(s => s.Saldo != 0) ?? cuenta.Saldos.FirstOrDefault();
                        if (saldoUnico != null)
                        {
                            for (int i = 0; i < cmbMonedaEfectivo.Items.Count; i++)
                                if (cmbMonedaEfectivo.Items[i] is ComboBoxItem ci && ci.Tag?.ToString() == saldoUnico.Moneda)
                                { cmbMonedaEfectivo.SelectedIndex = i; break; }
                            txtSaldoEfectivo.Text = saldoUnico.Saldo.ToString("N2");
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.Warn("CargarDatosCuentaEdicionAsync", ex); }
        }

        private async void BtnGuardar_Click(object? sender, RoutedEventArgs e)
        {
            string nombre = (txtNombre.Text?.Trim() ?? "").ToUpperInvariant();
            if (string.IsNullOrEmpty(nombre)) return;

            var itemTipo = cmbTipo.SelectedItem as ComboBoxItem;
            string tipo = itemTipo?.Content?.ToString() ?? "Efectivo";

            decimal? limiteDeuda = null;
            if (gridLimiteDeuda.IsVisible && decimal.TryParse(txtLimiteDeuda.Text, out var ld) && ld > 0)
                limiteDeuda = ld;

            var saldos = new List<SaldoCuentaDto>();
            if (tipo == "Efectivo")
            {
                var monedaSel = (cmbMonedaEfectivo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(monedaSel) && decimal.TryParse(txtSaldoEfectivo.Text, out var saldoEf))
                    saldos.Add(new SaldoCuentaDto { Moneda = monedaSel, Saldo = saldoEf });
            }
            else
            {
                foreach (var s in _saldosIniciales)
                    saldos.Add(new SaldoCuentaDto { Moneda = s.Moneda, Saldo = s.Saldo });
            }

            var request = new CrearCuentaRequest { Nombre = nombre, Tipo = tipo, LimiteDeuda = limiteDeuda, Saldos = saldos };

            try
            {
                if (_cuentaIdAEditar.HasValue)
                    await _apiClient.ActualizarCuentaAsync(_cuentaIdAEditar.Value, request);
                else
                    await _apiClient.CrearCuentaAsync(request);
                NotificationService.Success("Cuenta guardada", "Saldos actualizados correctamente.");
                Close();
            }
            catch (Exception ex) { NotificationService.Error("Error al guardar", ex.Message); }
        }

        private void BtnCancelar_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
