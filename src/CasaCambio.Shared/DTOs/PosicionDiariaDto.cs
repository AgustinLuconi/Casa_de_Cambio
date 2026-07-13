namespace CasaCambio.Shared.DTOs;

public class PosicionDiariaDto
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string TipoPase { get; set; } = "D";
    public decimal CapInicial { get; set; }
    public decimal CapFinal { get; set; }
}
