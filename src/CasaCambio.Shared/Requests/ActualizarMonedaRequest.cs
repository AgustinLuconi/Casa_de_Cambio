namespace CasaCambio.Shared.Requests;

public class ActualizarMonedaRequest
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public bool Activa { get; set; }
}
