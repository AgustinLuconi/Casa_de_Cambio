using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("cuentas")]
public class Cuenta
{
    [Key] [Column("id")] public int Id { get; set; }
    [Column("nombre")] public string Nombre { get; set; } = "";
    [Column("tipo")] public string Tipo { get; set; } = "Efectivo";
    [Column("limite_deuda")] public decimal? LimiteDeuda { get; set; }
    public List<SaldoCuenta> Saldos { get; set; } = new();
}

[Table("saldos_cuenta")]
public class SaldoCuenta
{
    [Key] [Column("id")] public int Id { get; set; }
    [Column("cuenta_id")] public int CuentaId { get; set; }
    [Column("moneda")] public string Moneda { get; set; } = "USD";
    [Column("saldo")] public decimal Saldo { get; set; }
    /// <summary>Límite de deuda específico para esta cuenta+divisa. 0 = hereda el límite general de la divisa.</summary>
    [Column("limite_deuda")] public decimal LimiteDeuda { get; set; }
    public Cuenta Cuenta { get; set; } = null!;
}
