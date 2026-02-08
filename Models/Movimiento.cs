using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.Models
{
    [Table("movimientos")]
    public class Movimiento
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("operacion_id")]
        public int OperacionId { get; set; }

        [ForeignKey("OperacionId")]
        public Operacion Operacion { get; set; } = null!;

        [Column("cuenta_id")]
        public int CuentaId { get; set; }

        [ForeignKey("CuentaId")]
        public Cuenta Cuenta { get; set; } = null!;

        [Column("monto")]
        public decimal Monto { get; set; } // Positivo = ingreso, Negativo = egreso

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;
    }
}
