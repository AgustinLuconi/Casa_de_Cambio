using System.ComponentModel.DataAnnotations;

namespace CasaCambio.Shared.Requests;

public class CrearCuentaRequest
{
    [Required] public string Nombre { get; set; } = "";
    [Required] public string Tipo { get; set; } = "Caja";
}
