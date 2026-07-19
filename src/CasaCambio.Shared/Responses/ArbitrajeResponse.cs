namespace CasaCambio.Shared.Responses;

public class ArbitrajeResponse
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionIdCompra { get; set; }
    public int? OperacionIdVenta { get; set; }

    public static ArbitrajeResponse Success(int idCompra, int idVenta) => new() { Exitoso = true, OperacionIdCompra = idCompra, OperacionIdVenta = idVenta };
    public static ArbitrajeResponse Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}
