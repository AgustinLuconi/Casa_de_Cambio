using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.Models
{
    [Table("tenencias_moneda")]
    public class TenenciaMoneda
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("moneda_id")]
        public int MonedaId { get; set; }

        [ForeignKey("MonedaId")]
        public Moneda Moneda { get; set; } = null!;

        [Column("cantidad_total")]
        public decimal CantidadTotal { get; set; }  // Cantidad de divisa en inventario

        [Column("costo_total")]
        public decimal CostoTotal { get; set; }  // Costo total en moneda local (ARS)

        /// <summary>
        /// Costo Promedio Ponderado = CostoTotal / CantidadTotal
        /// </summary>
        [NotMapped]
        public decimal PPP => CantidadTotal > 0 ? CostoTotal / CantidadTotal : 0;
    }
}
