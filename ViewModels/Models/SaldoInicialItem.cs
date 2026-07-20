namespace SistemaCambio.ViewModels.Models
{
    public class SaldoInicialItem
    {
        public string Moneda { get; set; } = "";
        public string Nombre { get; set; } = "";
        public decimal Saldo { get; set; }
        /// <summary>Límite de deuda específico para esta divisa. 0 = hereda el límite general.</summary>
        public decimal LimiteDeudaPersonalizado { get; set; }
    }
}
