using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("configuracion_sistema")]
public class ConfiguracionSistema
{
    [Key] [Column("clave")] public string Clave { get; set; } = "";
    [Column("valor")] public string Valor { get; set; } = "";
    [Column("descripcion")] public string Descripcion { get; set; } = "";
}
