using System;
using System.ComponentModel.DataAnnotations;
using CasaCambio.Shared.Enums;

namespace SistemaCambio.LocalDb;

public class LocalOperacion
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TipoOperacion { get; set; } = "";
    public int CuentaOrigenId { get; set; }
    public int CuentaDestinoId { get; set; }
    public string MonedaOrigen { get; set; } = "";
    public string MonedaDestino { get; set; } = "";
    public decimal MontoOrigen { get; set; }
    public decimal MontoDestino { get; set; }
    public decimal CotizacionAplicada { get; set; }
    public int? ClienteId { get; set; }
    public string Observaciones { get; set; } = "";
    public DateTime FechaCreacionLocal { get; set; } = DateTime.UtcNow;

    // Sync state
    public EstadoSincronizacion EstadoSync { get; set; } = EstadoSincronizacion.Pendiente;
    public string? ErrorSync { get; set; }
    public int IntentosSyncCount { get; set; }
    public int? ServerOperacionId { get; set; }
    public DateTime? FechaSincronizacion { get; set; }
}
