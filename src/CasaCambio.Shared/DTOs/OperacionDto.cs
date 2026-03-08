namespace CasaCambio.Shared.DTOs;

public class OperacionDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string TipoOperacion { get; set; } = "";
    public int? ClienteId { get; set; }
    public string? NombreCliente { get; set; }
    public decimal MontoTotalOrigen { get; set; }
    public decimal MontoTotalDestino { get; set; }
    public decimal CotizacionAplicada { get; set; }
    public string Observaciones { get; set; } = "";
    public List<MovimientoDto> Movimientos { get; set; } = new();
}
