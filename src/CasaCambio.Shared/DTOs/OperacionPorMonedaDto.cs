namespace CasaCambio.Shared.DTOs;

public class OperacionPorMonedaDto
{
    public string Moneda { get; set; } = "";
    public int CantidadOperaciones { get; set; }
    public decimal VolumenTotal { get; set; }
}
