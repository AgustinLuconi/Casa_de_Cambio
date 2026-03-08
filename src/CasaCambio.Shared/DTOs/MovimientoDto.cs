namespace CasaCambio.Shared.DTOs;

public class MovimientoDto
{
    public int Id { get; set; }
    public int OperacionId { get; set; }
    public int CuentaId { get; set; }
    public string NombreCuenta { get; set; } = "";
    public string Moneda { get; set; } = "";
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
}
