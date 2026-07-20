using CommunityToolkit.Mvvm.ComponentModel;
using SistemaCambio.Services;

namespace SistemaCambio.ViewModels
{
    public partial class PosicionDiariaItem : ObservableObject
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string TipoPase { get; set; } = "D";
        public decimal CapInicial { get; set; }
        public decimal CapFinal { get; set; }

        [ObservableProperty] private string _cotInicialTexto = "0.00000";
        [ObservableProperty] private string _cotFinalTexto = "0.00000";
        [ObservableProperty] private string _usdInicialFormatted = "0.00";
        [ObservableProperty] private string _usdFinalFormatted = "0.00";
        [ObservableProperty] private string _gananciaFormatted = "0.00";
        [ObservableProperty] private decimal _ganancia;

        partial void OnCotInicialTextoChanged(string value) => Recalcular();
        partial void OnCotFinalTextoChanged(string value) => Recalcular();

        private void Recalcular()
        {
            decimal cotInicial = MontoHelper.Parsear(CotInicialTexto);
            decimal cotFinal = MontoHelper.Parsear(CotFinalTexto);
            decimal usdInicial = ConvertirAUsd(CapInicial, cotInicial, TipoPase);
            decimal usdFinal = ConvertirAUsd(CapFinal, cotFinal, TipoPase);
            decimal ganancia = usdFinal - usdInicial;

            UsdInicialFormatted = usdInicial.ToString("N2");
            UsdFinalFormatted = usdFinal.ToString("N2");
            GananciaFormatted = ganancia.ToString("N2");
            Ganancia = ganancia;
        }

        private static decimal ConvertirAUsd(decimal capital, decimal cotizacion, string tipoPase)
        {
            if (cotizacion == 0) return 0;
            return tipoPase == "M" ? capital * cotizacion : capital / cotizacion;
        }
    }
}
