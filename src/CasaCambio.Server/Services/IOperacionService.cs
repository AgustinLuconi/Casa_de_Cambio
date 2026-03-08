namespace CasaCambio.Server.Services;

public class OperacionResult
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionId { get; set; }
    public static OperacionResult Success(int id) => new() { Exitoso = true, OperacionId = id };
    public static OperacionResult Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}

public interface IOperacionService
{
    OperacionResult GuardarOperacion(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, int? clienteId = null, string observaciones = "");
    OperacionResult GuardarOperacionInterbancaria(string tipo, int cuentaOrigenId, int cuentaDestinoId, string monedaOrigen, string monedaDestino, decimal montoOrigen, decimal montoDestino, decimal cotizacion, string observaciones = "");
    OperacionResult GuardarCreditoDebito(int cuentaCreditoId, int cuentaDebitoId, string monedaCredito, string monedaDebito, decimal montoCredito, decimal montoDebito, decimal cotizacion, int? clienteId = null, string observaciones = "");
}
