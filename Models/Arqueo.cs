using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.Models
{
    [Table("arqueos")]
    public class Arqueo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column("cuenta_id")]
        public int CuentaId { get; set; }

        [ForeignKey("CuentaId")]
        public Cuenta Cuenta { get; set; } = null!;

        [Column("saldo_sistema")]
        public decimal SaldoSistema { get; set; }

        [Column("saldo_arqueo")]
        public decimal SaldoArqueo { get; set; }

        [Column("diferencia")]
        public decimal Diferencia { get; set; }  // Positivo = Sobrante, Negativo = Faltante

        [Column("movimiento_ajuste_id")]
        public int? MovimientoAjusteId { get; set; }

        [ForeignKey("MovimientoAjusteId")]
        public Movimiento? MovimientoAjuste { get; set; }

        [Column("observaciones")]
        public string Observaciones { get; set; } = "";
    }
}
