using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("tenencias_moneda")]
public class TenenciaMoneda
{
    [Key] [Column("id")] public int Id { get; set; }
    [Column("moneda_id")] public int MonedaId { get; set; }
    [ForeignKey("MonedaId")] public Moneda Moneda { get; set; } = null!;
    [Column("cantidad_total")] public decimal CantidadTotal { get; set; }
    [Column("costo_total")] public decimal CostoTotal { get; set; }
    [NotMapped] public decimal PPP => CantidadTotal > 0 ? CostoTotal / CantidadTotal : 0;
}
