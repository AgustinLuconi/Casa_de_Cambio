using System.ComponentModel.DataAnnotations;

namespace CasaCambio.Shared.Requests;

public class CrearArqueoRequest
{
    [Required] public int CuentaId { get; set; }
    [Required] public string Moneda { get; set; } = "";
    [Range(0, double.MaxValue)] public decimal SaldoArqueo { get; set; }
    public string Observaciones { get; set; } = "";
}
