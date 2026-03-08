namespace CasaCambio.Shared.Requests;

public class SyncPushRequest
{
    public List<OperacionOfflineRequest> Operaciones { get; set; } = new();
}

public class OperacionOfflineRequest
{
    public string LocalId { get; set; } = "";
    public string TipoOperacion { get; set; } = "";
    public int CuentaOrigenId { get; set; }
    public int CuentaDestinoId { get; set; }
    public string MonedaOrigen { get; set; } = "";
    public string MonedaDestino { get; set; } = "";
    public decimal MontoOrigen { get; set; }
    public decimal MontoDestino { get; set; }
    public decimal CotizacionAplicada { get; set; }
    public int? ClienteId { get; set; }
    public string Observaciones { get; set; } = "";
    public DateTime FechaCreacionLocal { get; set; }
}
