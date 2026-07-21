using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SistemaCambio.ApiClient;
using SistemaCambio.Services;

namespace SistemaCambio.ViewModels
{
    public partial class DetalleCuentaViewModel : ViewModelBase
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly int _cuentaId;
        private readonly IDialogService _dialogService;

        [ObservableProperty] private string _nombreCuenta = "Nombre de la Cuenta";
        [ObservableProperty] private string _tipoCuenta = "Tipo";
        [ObservableProperty] private bool _sinMovimientos;

        public ObservableCollection<DetalleSaldoItem> Saldos { get; } = new();
        public ObservableCollection<MovimientoDisplay> Movimientos { get; } = new();

        public event Action? SolicitarCierre;

        public DetalleCuentaViewModel(ICasaCambioApiClient apiClient, int cuentaId, IDialogService dialogService)
        {
            _apiClient = apiClient;
            _cuentaId = cuentaId;
            _dialogService = dialogService;

            _ = CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            try
            {
                var cuentas = await _apiClient.ObtenerCuentasAsync();
                var cuenta = cuentas.FirstOrDefault(c => c.Id == _cuentaId);

                if (cuenta == null)
                {
                    await _dialogService.MensajeAsync("Cuenta no encontrada", "No se pudo encontrar la cuenta solicitada.");
                    SolicitarCierre?.Invoke();
                    return;
                }

                NombreCuenta = cuenta.Nombre;
                TipoCuenta = cuenta.Tipo;

                string[] colors = { "#3B82F6", "#16A34A", "#D97706", "#DC2626", "#8b5cf6", "#14b8a6" };
                int brushIndex = 0;
                foreach (var saldo in cuenta.Saldos.OrderBy(s => s.Moneda))
                {
                    Saldos.Add(new DetalleSaldoItem
                    {
                        Moneda = saldo.Moneda,
                        SaldoFormatted = $"{saldo.Saldo:N2}",
                        ColorHex = colors[brushIndex % colors.Length]
                    });
                    brushIndex++;
                }

                var movimientosPage = await _apiClient.ObtenerMovimientosCuentaAsync(_cuentaId);
                foreach (var m in movimientosPage.Items)
                {
                    string prefijo = m.Monto > 0 ? "+" : "";
                    Movimientos.Add(new MovimientoDisplay
                    {
                        Fecha = m.Fecha,
                        CodigoOperacion = $"OP-{m.OperacionId:D5}",
                        Moneda = m.Moneda,
                        MontoFormatted = $"{prefijo}{m.Monto:N2}",
                        Monto = m.Monto
                    });
                }

                if (Movimientos.Count == 0) SinMovimientos = true;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("CargarDatosAsync", ex);
                await _dialogService.MensajeAsync("Error al cargar detalle de cuenta", ex.Message);
                SolicitarCierre?.Invoke();
            }
        }
    }

    public class MovimientoDisplay
    {
        public DateTime Fecha { get; set; }
        public string CodigoOperacion { get; set; } = "";
        public string Moneda { get; set; } = "";
        public string MontoFormatted { get; set; } = "";
        public decimal Monto { get; set; }
    }

    public class DetalleSaldoItem
    {
        public string Moneda { get; set; } = "";
        public string SaldoFormatted { get; set; } = "";
        public string ColorHex { get; set; } = "";
    }
}
