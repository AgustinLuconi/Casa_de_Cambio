using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("cierres_caja")]
public class CierreCaja
{
    [Key] [Column("id")] public int Id { get; set; }
    [Column("fecha")] public DateTime Fecha { get; set; } = DateTime.Today;
    [Column("fecha_cierre")] public DateTime FechaCierre { get; set; } = DateTime.Now;
    [Column("usuario")] public string Usuario { get; set; } = "Admin";
    [Column("cantidad_compras")] public int CantidadCompras { get; set; }
    [Column("total_compras_usd")] public decimal TotalComprasUSD { get; set; }
    [Column("total_compras_ars")] public decimal TotalComprasARS { get; set; }
    [Column("cantidad_ventas")] public int CantidadVentas { get; set; }
    [Column("total_ventas_usd")] public decimal TotalVentasUSD { get; set; }
    [Column("total_ventas_ars")] public decimal TotalVentasARS { get; set; }
    [Column("saldo_caja_ars")] public decimal SaldoCajaARS { get; set; }
    [Column("saldo_caja_usd")] public decimal SaldoCajaUSD { get; set; }
    [Column("saldo_caja_eur")] public decimal SaldoCajaEUR { get; set; }
    [Column("total_diferencias")] public decimal TotalDiferencias { get; set; }
    [Column("observaciones")] public string Observaciones { get; set; } = "";
    [Column("cerrado")] public bool Cerrado { get; set; } = false;
}
