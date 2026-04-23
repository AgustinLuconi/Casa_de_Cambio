using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SistemaCambio.Views
{
    public partial class ArqueoItemViewModel : ObservableObject
    {
        public string CodigoMoneda { get; set; } = "";
        public string NombreMoneda { get; set; } = "";
        public decimal SaldoSistema { get; set; }
        public List<(int CuentaId, decimal Saldo)> Cajas { get; set; } = new();

        [ObservableProperty] private decimal arqueoFisico;
        [ObservableProperty] private decimal diferencia;

        partial void OnArqueoFisicoChanged(decimal value)
        {
            Diferencia = value - SaldoSistema;
        }
    }

    public partial class ArqueoWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly ObservableCollection<ArqueoItemViewModel> _items = new();

        public ArqueoWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            InitializeComponent();
            NotificationService.Initialize(notificationPanel);
            Closed += (_, _) => (Owner as MainWindow)?.RestaurarNotificationPanel();
            dgArqueo.ItemsSource = _items;
            CargarDatosAsync();
        }

        private async void CargarDatosAsync()
        {
            try
            {
                _items.Clear();
                var cuentasTask = _apiClient.ObtenerCuentasAsync();
                var monedasTask = _apiClient.ObtenerMonedasAsync();
                await System.Threading.Tasks.Task.WhenAll(cuentasTask, monedasTask);

                var cajas = cuentasTask.Result.Where(c => c.Tipo == "Efectivo").ToList();
                var catalogoMonedas = monedasTask.Result;

                var agregados = cajas
                    .SelectMany(c => c.Saldos.Select(s => new { Cuenta = c, Saldo = s }))
                    .GroupBy(x => x.Saldo.Moneda)
                    .OrderBy(g => g.Key)
                    .Select(g => new ArqueoItemViewModel
                    {
                        CodigoMoneda = g.Key,
                        NombreMoneda = catalogoMonedas.FirstOrDefault(m => m.Codigo == g.Key)?.Nombre ?? g.Key,
                        SaldoSistema = g.Sum(x => x.Saldo.Saldo),
                        ArqueoFisico = g.Sum(x => x.Saldo.Saldo),
                        Cajas = g.Select(x => (x.Cuenta.Id, x.Saldo.Saldo)).ToList()
                    });

                foreach (var item in agregados)
                    _items.Add(item);
            }
            catch (Exception ex) { NotificationService.Error("Error al cargar datos", ex.Message); }
        }

        private async void BtnAceptar_Click(object? sender, RoutedEventArgs e)
        {
            var itemsConDiferencia = _items.Where(i => i.Diferencia != 0 && i.Cajas.Count > 0).ToList();
            if (itemsConDiferencia.Count == 0)
            {
                NotificationService.Success("Arqueo completado", "Sin diferencias — caja cuadra perfectamente.");
                Close();
                return;
            }

            int ajustes = 0;
            foreach (var item in itemsConDiferencia)
            {
                var primeraCaja = item.Cajas.First();
                decimal nuevoSaldoPrimera = primeraCaja.Saldo + item.Diferencia;
                try
                {
                    await _apiClient.RealizarArqueoAsync(new CrearArqueoRequest
                    {
                        CuentaId = primeraCaja.CuentaId,
                        Moneda = item.CodigoMoneda,
                        SaldoArqueo = nuevoSaldoPrimera,
                        Observaciones = item.Diferencia > 0 ? "Sobrante de caja" : "Faltante de caja"
                    });
                    ajustes++;
                }
                catch (Exception ex)
                {
                    NotificationService.Error($"Error en {item.CodigoMoneda}", ex.Message);
                    return;
                }
            }

            decimal totalDiferencia = itemsConDiferencia.Sum(i => i.Diferencia);
            if (totalDiferencia > 0)
                NotificationService.Warning("Arqueo completado", $"Sobrante: ${totalDiferencia:N2} ({ajustes} ajuste(s))");
            else if (totalDiferencia < 0)
                NotificationService.Warning("Arqueo completado", $"Faltante: ${Math.Abs(totalDiferencia):N2} ({ajustes} ajuste(s))");
            else
                NotificationService.Success("Arqueo completado", $"{ajustes} ajuste(s) realizado(s)");
            Close();
        }

        private void BtnSalir_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
