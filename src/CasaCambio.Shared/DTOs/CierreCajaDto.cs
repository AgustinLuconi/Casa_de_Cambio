namespace CasaCambio.Shared.DTOs;

public class CierreCajaDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public DateTime FechaCierre { get; set; }
    public string Usuario { get; set; } = "";
    public int CantidadCompras { get; set; }
    public decimal TotalComprasUSD { get; set; }
    public decimal TotalComprasARS { get; set; }
    public int CantidadVentas { get; set; }
    public decimal TotalVentasUSD { get; set; }
    public decimal TotalVentasARS { get; set; }
    public decimal SaldoCajaARS { get; set; }
    public decimal SaldoCajaUSD { get; set; }
    public decimal SaldoCajaEUR { get; set; }
    public decimal TotalDiferencias { get; set; }
    public string Observaciones { get; set; } = "";
    public bool Cerrado { get; set; }
}
