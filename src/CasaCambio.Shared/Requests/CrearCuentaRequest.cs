using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CasaCambio.Shared.DTOs;

namespace CasaCambio.Shared.Requests;

public class CrearCuentaRequest
{
    [Required] public string Nombre { get; set; } = "";
    [Required] public string Tipo { get; set; } = "Efectivo";
    public decimal? LimiteDeuda { get; set; }
    public List<SaldoCuentaDto> Saldos { get; set; } = new();
}
