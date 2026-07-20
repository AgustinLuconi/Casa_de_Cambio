namespace SistemaCambio.ViewModels.Models
{
    public class CuentaMonedaTag
    {
        public int CuentaId { get; set; }
        public string Moneda { get; set; } = "";
        public string NombreCuenta { get; set; } = "";
        public override string ToString() => NombreCuenta;
    }
}
