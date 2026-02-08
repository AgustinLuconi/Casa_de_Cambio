using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.Models
{
    [Table("cotizaciones_diarias")]
    public class CotizacionDiaria
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("moneda_id")]
        public int MonedaId { get; set; }

        [ForeignKey("MonedaId")]
        public Moneda Moneda { get; set; } = null!;

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [Column("cotizacion_compra")]
        public decimal CotizacionCompra { get; set; }

        [Column("cotizacion_venta")]
        public decimal CotizacionVenta { get; set; }
    }
}
