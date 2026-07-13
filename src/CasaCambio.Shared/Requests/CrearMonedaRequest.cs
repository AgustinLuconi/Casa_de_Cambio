using System.ComponentModel.DataAnnotations;

namespace CasaCambio.Shared.Requests;

public class CrearMonedaRequest
{
    [Required] [MaxLength(10)] public string Codigo { get; set; } = "";
    [Required] [MaxLength(100)] public string Nombre { get; set; } = "";
    public string TipoPase { get; set; } = "D";
}
