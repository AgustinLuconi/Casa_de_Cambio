using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("estados_caja")]
public class EstadoCaja
{
    [Key] [Column("id")] public int Id { get; set; }
    [Column("cuenta_id")] public int CuentaId { get; set; }
    [ForeignKey("CuentaId")] public Cuenta Cuenta { get; set; } = null!;
    [Column("fecha_apertura")] public DateTime FechaApertura { get; set; } = DateTime.Now;
    [Column("saldo_apertura")] public decimal SaldoApertura { get; set; }
    [Column("fecha_cierre")] public DateTime? FechaCierre { get; set; }
    [Column("saldo_cierre")] public decimal? SaldoCierre { get; set; }
    [Column("arqueo_id")] public int? ArqueoId { get; set; }
    [ForeignKey("ArqueoId")] public Arqueo? Arqueo { get; set; }
    [Column("estado")] public string Estado { get; set; } = "Abierta";
    [Column("usuario_apertura")] public string? UsuarioApertura { get; set; }
    [Column("usuario_cierre")] public string? UsuarioCierre { get; set; }
}
