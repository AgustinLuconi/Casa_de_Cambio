using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("operaciones")]
public class Operacion
{
    [Key] [Column("id")] public int Id { get; set; }
    [Column("fecha")] public DateTime Fecha { get; set; } = DateTime.Now;
    [Column("tipo_operacion")] public string TipoOperacion { get; set; } = "Compra";
    [Column("monto_total_origen")] public decimal MontoTotalOrigen { get; set; }
    [Column("monto_total_destino")] public decimal MontoTotalDestino { get; set; }
    [Column("cotizacion_aplicada")] public decimal CotizacionAplicada { get; set; }
    [Column("observaciones")] public string Observaciones { get; set; } = "";
    [Column("idempotency_key")] public string? IdempotencyKey { get; set; }
    [Column("anulada")] public bool Anulada { get; set; } = false;
    [Column("operacion_original_id")] public int? OperacionOriginalId { get; set; }
    [ForeignKey("OperacionOriginalId")] public Operacion? OperacionOriginal { get; set; }
    [Column("operacion_pareja_id")] public int? OperacionParejaId { get; set; }
    [ForeignKey("OperacionParejaId")] public Operacion? OperacionPareja { get; set; }
    public List<Movimiento> Movimientos { get; set; } = new();
}
