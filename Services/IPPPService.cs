namespace SistemaCambio.Services
{
    public interface IPPPService
    {
        void RegistrarCompra(string codigoMoneda, decimal cantidadDivisa, decimal costoEnPesos);
        void RegistrarVenta(string codigoMoneda, decimal cantidadDivisa);
        PPPValidacion ValidarVenta(string codigoMoneda, decimal cotizacionVenta);
        decimal ObtenerPPP(string codigoMoneda);
    }
}
