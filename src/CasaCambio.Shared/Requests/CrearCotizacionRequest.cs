using System.ComponentModel.DataAnnotations;

namespace CasaCambio.Shared.Requests;

public class CrearCotizacionRequest
{
    [Required] public string CodigoMoneda { get; set; } = "";
    [Range(0.00001, double.MaxValue)] public decimal CotizacionCompra { get; set; }
    [Range(0.00001, double.MaxValue)] public decimal CotizacionVenta { get; set; }
}
