namespace CasaCambio.Shared.DTOs;

public class CuentaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = "Caja";
    public List<SaldoCuentaDto> Saldos { get; set; } = new();
}
