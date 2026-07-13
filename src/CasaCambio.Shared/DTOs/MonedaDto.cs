namespace CasaCambio.Shared.DTOs;

public class MonedaDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public bool Activa { get; set; }
    public string TipoPase { get; set; } = "D";
}
