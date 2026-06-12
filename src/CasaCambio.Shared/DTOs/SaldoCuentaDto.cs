namespace CasaCambio.Shared.DTOs;

public class SaldoCuentaDto
{
    public string Moneda { get; set; } = "";
    public decimal Saldo { get; set; }
    /// <summary>Límite de deuda específico para esta cuenta+divisa. 0 = hereda el límite general de la divisa.</summary>
    public decimal LimiteDeudaPersonalizado { get; set; }
}
