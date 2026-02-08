using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.Models
{
    [Table("operaciones")]
    public class Operacion
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column("tipo_operacion")]
        public string TipoOperacion { get; set; } = "Compra"; // "Compra" o "Venta"

        [Column("cliente_id")]
        public int? ClienteId { get; set; }

        [ForeignKey("ClienteId")]
        public Cliente? Cliente { get; set; }

        [Column("monto_total_origen")]
        public decimal MontoTotalOrigen { get; set; }

        [Column("monto_total_destino")]
        public decimal MontoTotalDestino { get; set; }

        [Column("cotizacion_aplicada")]
        public decimal CotizacionAplicada { get; set; }

        [Column("observaciones")]
        public string Observaciones { get; set; } = "";

        // Navegación: una operación tiene muchos movimientos
        public List<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
    }
}
