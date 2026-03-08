namespace CasaCambio.Shared.DTOs;

public class CotizacionDto
{
    public int Id { get; set; }
    public string CodigoMoneda { get; set; } = "";
    public DateTime Fecha { get; set; }
    public decimal CotizacionCompra { get; set; }
    public decimal CotizacionVenta { get; set; }
}
