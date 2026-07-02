namespace CasaCambio.Shared.DTOs;

public class OperacionDto
{
    public int Id { get; set; }
    public string CodigoOperacion => $"OP-{Id:D5}";
    public DateTime Fecha { get; set; }
    public string TipoOperacion { get; set; } = "";
    public decimal MontoTotalOrigen { get; set; }
    public decimal MontoTotalDestino { get; set; }
    public decimal CotizacionAplicada { get; set; }
    public string Observaciones { get; set; } = "";
    public bool Anulada { get; set; }
    public int? OperacionOriginalId { get; set; }
    public string? CodigoOriginal => OperacionOriginalId.HasValue ? $"OP-{OperacionOriginalId.Value:D5}" : null;
    public bool PuedeAnular => !Anulada && !OperacionOriginalId.HasValue;
    public string EstadoDisplay => Anulada ? "ANULADA" : OperacionOriginalId.HasValue ? "ANULACIÓN" : "";
    public List<MovimientoDto> Movimientos { get; set; } = new();
}
