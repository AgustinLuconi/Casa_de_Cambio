namespace SistemaCambio.ViewModels.Models
{
    // Fila aplanada para la grilla de "Saldos por Cuenta": expone Moneda y Saldo
    // como propiedades de primer nivel que el DataGrid puede bindear directamente.
    public class SaldoReporteRow
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Moneda { get; set; } = "";
        public decimal Saldo { get; set; }
    }
}
