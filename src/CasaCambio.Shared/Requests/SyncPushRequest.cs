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
    public string Observaciones { get; set; } = "";
    public DateTime FechaCreacionLocal { get; set; }

    // Campos específicos de Arbitraje (TipoOperacion == "Arbitraje"). Null/0 para los demás tipos.
    // Reutiliza CuentaOrigenId=CuentaPesosId, MonedaOrigen="ARS", MontoOrigen=PesosCompra,
    // CuentaDestinoId=CuentaAcreditaCompraId, MonedaDestino=MonedaCompra, MontoDestino=MontoExtranjeroCompra,
    // CotizacionAplicada=CotizacionCompra (la pata Compra ya encaja en la forma existente).
    public int CuentaDebitaVentaId { get; set; }
    public string MonedaVenta { get; set; } = "";
    public decimal MontoExtranjeroVenta { get; set; }
    public decimal CotizacionVenta { get; set; }
    public string TipoOperacionArbitraje { get; set; } = "";
}
