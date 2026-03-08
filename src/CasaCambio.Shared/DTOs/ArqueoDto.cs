namespace CasaCambio.Shared.DTOs;

public class ArqueoDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int CuentaId { get; set; }
    public string NombreCuenta { get; set; } = "";
    public decimal SaldoSistema { get; set; }
    public decimal SaldoArqueo { get; set; }
    public decimal Diferencia { get; set; }
    public string Observaciones { get; set; } = "";
}
