namespace CasaCambio.Server.Services;

public class PPPValidacion
{
    public bool Valido { get; set; }
    public decimal PPP { get; set; }
    public decimal CotizacionVenta { get; set; }
    public string Mensaje { get; set; } = "";
}

public interface IPPPService
{
    void RegistrarCompra(string codigoMoneda, decimal cantidadDivisa, decimal costoEnPesos);
    void RegistrarVenta(string codigoMoneda, decimal cantidadDivisa);
    PPPValidacion ValidarVenta(string codigoMoneda, decimal cotizacionVenta);
    decimal ObtenerPPP(string codigoMoneda);
}
