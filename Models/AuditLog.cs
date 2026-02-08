using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.Models
{
    [Table("audit_logs")]
    public class AuditLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column("usuario_id")]
        public int? UsuarioId { get; set; }

        [Column("usuario_nombre")]
        [MaxLength(100)]
        public string UsuarioNombre { get; set; } = "";

        [Column("accion")]
        [MaxLength(20)]
        public string Accion { get; set; } = "";  // "CREATE", "UPDATE", "DELETE", "AJUSTE"

        [Column("entidad")]
        [MaxLength(50)]
        public string Entidad { get; set; } = "";  // "Operacion", "CotizacionDiaria", "Arqueo", etc.

        [Column("entidad_id")]
        public int EntidadId { get; set; }

        [Column("valores_anteriores")]
        public string? ValoresAnteriores { get; set; }  // JSON con valores antes del cambio

        [Column("valores_nuevos")]
        public string? ValoresNuevos { get; set; }  // JSON con valores después del cambio

        [Column("ip_address")]
        [MaxLength(45)]
        public string? IpAddress { get; set; }
    }
}
