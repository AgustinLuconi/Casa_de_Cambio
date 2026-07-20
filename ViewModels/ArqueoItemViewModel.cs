using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SistemaCambio.ViewModels
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
}
