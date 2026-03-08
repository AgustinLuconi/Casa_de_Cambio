namespace CasaCambio.Shared.Responses;

public class OperacionResponse
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? OperacionId { get; set; }

    public static OperacionResponse Success(int id) => new() { Exitoso = true, OperacionId = id };
    public static OperacionResponse Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}
