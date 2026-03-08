using System.ComponentModel.DataAnnotations;

namespace CasaCambio.Shared.Requests;

public class CrearInterbancarioRequest
{
    [Required] public int CuentaOrigenId { get; set; }
    [Required] public int CuentaDestinoId { get; set; }
    [Required] public string MonedaOrigen { get; set; } = "";
    [Required] public string MonedaDestino { get; set; } = "";
    [Range(0.01, double.MaxValue)] public decimal MontoOrigen { get; set; }
    [Range(0.01, double.MaxValue)] public decimal MontoDestino { get; set; }
    [Range(0.00001, double.MaxValue)] public decimal Cotizacion { get; set; }
    public string Observaciones { get; set; } = "";
}
