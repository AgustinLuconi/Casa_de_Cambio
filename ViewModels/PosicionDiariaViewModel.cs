using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;

namespace SistemaCambio.ViewModels
{
    public partial class PosicionDiariaViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;

        [ObservableProperty] private DateTimeOffset? _fechaDesde = new DateTimeOffset(DateTime.Today);
        [ObservableProperty] private DateTimeOffset? _fechaHasta = new DateTimeOffset(DateTime.Today);
        [ObservableProperty] private bool _sinDatos;

        public ObservableCollection<PosicionDiariaItem> Items { get; } = new();

        public ICommand BuscarCommand { get; }

        public PosicionDiariaViewModel(ICasaCambioApiClient apiClient)
        {
            _apiClient = apiClient;
            BuscarCommand = new AsyncRelayCommand(BuscarAsync);
            _ = BuscarAsync();
        }

        private async Task BuscarAsync()
        {
            var desde = FechaDesde?.DateTime ?? DateTime.Today;
            var hasta = FechaHasta?.DateTime ?? DateTime.Today;
            if (hasta.Date < desde.Date)
            {
                NotificationService.Warning("Rango inválido", "La fecha 'Hasta' no puede ser anterior a 'Desde'.");
                return;
            }

            try
            {
                var posiciones = await _apiClient.ObtenerPosicionDiariaAsync(desde, hasta);
                Items.Clear();
                foreach (var p in posiciones)
                {
                    Items.Add(new PosicionDiariaItem
                    {
                        Codigo = p.Codigo,
                        Nombre = p.Nombre,
                        TipoPase = p.TipoPase,
                        CapInicial = p.CapInicial,
                        CapFinal = p.CapFinal
                    });
                }
                SinDatos = Items.Count == 0;
            }
            catch (Exception ex) { NotificationService.Error("Error", ex.Message); }
        }
    }
}
