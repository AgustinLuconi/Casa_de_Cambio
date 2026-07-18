namespace CasaCambio.Server.Services;

public class OperacionResult
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionId { get; set; }
    public static OperacionResult Success(int id) => new() { Exitoso = true, OperacionId = id };
    public static OperacionResult Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}

public class ArbitrajeResult
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionIdCompra { get; set; }
    public int? OperacionIdVenta { get; set; }
    public static ArbitrajeResult Success(int idCompra, int idVenta) => new() { Exitoso = true, OperacionIdCompra = idCompra, OperacionIdVenta = idVenta };
    public static ArbitrajeResult Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}

public interface IOperacionService
{
    OperacionResult GuardarOperacion(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, string observaciones = "", string? idempotencyKey = null);
    OperacionResult GuardarOperacionInterbancaria(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, string observaciones = "", string? idempotencyKey = null);
    OperacionResult GuardarCreditoDebito(int cuentaCreditoId, int cuentaDebitoId, string monedaCredito, string monedaDebito, decimal montoCredito, decimal montoDebito, decimal cotizacion, string observaciones = "", string? idempotencyKey = null);
    ArbitrajeResult GuardarArbitraje(string monedaCompra, int cuentaAcreditaCompraId, decimal montoExtranjeroCompra, decimal cotizacionCompra, decimal pesosCompra, string monedaVenta, int cuentaDebitaVentaId, decimal montoExtranjeroVenta, decimal cotizacionVenta, decimal pesosVenta, int cuentaPesosId, string tipoOperacion, string observaciones = "");
    OperacionResult AnularOperacion(int id);
}
