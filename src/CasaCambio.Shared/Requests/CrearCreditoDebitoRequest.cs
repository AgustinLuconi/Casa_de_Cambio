using System.ComponentModel.DataAnnotations;

namespace CasaCambio.Shared.Requests;

public class CrearCreditoDebitoRequest
{
    [Required] public int CuentaCreditoId { get; set; }
    [Required] public int CuentaDebitoId { get; set; }
    [Required] public string MonedaCredito { get; set; } = "";
    [Required] public string MonedaDebito { get; set; } = "";
    [Range(0.01, double.MaxValue)] public decimal MontoCredito { get; set; }
    [Range(0.01, double.MaxValue)] public decimal MontoDebito { get; set; }
    [Range(0.00001, double.MaxValue)] public decimal Cotizacion { get; set; }
    public string Observaciones { get; set; } = "";
    public string? IdempotencyKey { get; set; }
}
