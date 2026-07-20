using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;

namespace SistemaCambio.ViewModels
{
    public partial class ArqueoViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;

        [ObservableProperty] private ObservableCollection<ArqueoItemViewModel> _items = new();

        public ICommand AceptarCommand { get; }

        public event Action? SolicitarCierre;

        public ArqueoViewModel(ICasaCambioApiClient apiClient)
        {
            _apiClient = apiClient;
            AceptarCommand = new AsyncRelayCommand(AceptarAsync);
            _ = CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            try
            {
                var nuevosItems = new ObservableCollection<ArqueoItemViewModel>();
                var cuentasTask = _apiClient.ObtenerCuentasAsync();
                var monedasTask = _apiClient.ObtenerMonedasAsync();
                await Task.WhenAll(cuentasTask, monedasTask);

                var cajas = cuentasTask.Result.Where(c => c.Tipo == "Efectivo").ToList();
                var catalogoMonedas = monedasTask.Result;   // ya filtradas por Activa en el server

                // Indexar los saldos de caja por moneda una sola vez (lado opcional del left join)
                var saldosPorMoneda = cajas
                    .SelectMany(c => c.Saldos.Select(s => new { Cuenta = c, s.Moneda, s.Saldo }))
                    .GroupBy(x => x.Moneda)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Recorrer TODAS las monedas activas del catálogo, no solo las que tienen saldo.
                // Una moneda sin saldo en caja igual aparece con Saldo/Arqueo/Diferencia en 0,00.
                foreach (var moneda in catalogoMonedas.OrderBy(m => m.Codigo))
                {
                    saldosPorMoneda.TryGetValue(moneda.Codigo, out var entradas);
                    decimal saldoSistema = entradas?.Sum(x => x.Saldo) ?? 0m;
                    var cajasMoneda = entradas?.Select(x => (x.Cuenta.Id, x.Saldo)).ToList()
                                      ?? new List<(int, decimal)>();

                    nuevosItems.Add(new ArqueoItemViewModel
                    {
                        CodigoMoneda = moneda.Codigo,
                        NombreMoneda = moneda.Nombre,
                        SaldoSistema = saldoSistema,
                        ArqueoFisico = saldoSistema,
                        Cajas = cajasMoneda
                    });
                }

                Items = nuevosItems;
            }
            catch (Exception ex) { NotificationService.Error("Error al cargar datos", ex.Message); }
        }

        private async Task AceptarAsync()
        {
            var itemsConDiferencia = Items.Where(i => i.Diferencia != 0 && i.Cajas.Count > 0).ToList();
            if (itemsConDiferencia.Count == 0)
            {
                NotificationService.Success("Arqueo completado", "Sin diferencias — caja cuadra perfectamente.");
                SolicitarCierre?.Invoke();
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
            SolicitarCierre?.Invoke();
        }
    }
}
