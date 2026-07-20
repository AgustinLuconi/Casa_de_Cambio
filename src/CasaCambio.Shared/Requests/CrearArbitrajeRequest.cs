using System.ComponentModel.DataAnnotations;

namespace CasaCambio.Shared.Requests;

public class CrearArbitrajeRequest
{
    [Required] public string MonedaCompra { get; set; } = "";
    [Required] public int CuentaAcreditaCompraId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal MontoExtranjeroCompra { get; set; }
    [Range(0.00001, double.MaxValue)] public decimal CotizacionCompra { get; set; }
    [Range(0.01, double.MaxValue)] public decimal PesosCompra { get; set; }

    [Required] public string MonedaVenta { get; set; } = "";
    [Required] public int CuentaDebitaVentaId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal MontoExtranjeroVenta { get; set; }
    [Range(0.00001, double.MaxValue)] public decimal CotizacionVenta { get; set; }
    [Range(0.01, double.MaxValue)] public decimal PesosVenta { get; set; }

    [Required] public int CuentaPesosId { get; set; }
    public string TipoOperacion { get; set; } = "CLIENTE";
    public string Observaciones { get; set; } = "";
    public string? IdempotencyKey { get; set; }
}
