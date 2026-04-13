using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.LocalDb;

public class CacheCuenta
{
    [Key]
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = "";
    public List<CacheSaldo> Saldos { get; set; } = new();
}

public class CacheSaldo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int CuentaId { get; set; }
    public string Moneda { get; set; } = "";
    public decimal Saldo { get; set; }
    public CacheCuenta Cuenta { get; set; } = null!;
}

public class CacheMoneda
{
    [Key]
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public bool Activa { get; set; }
}

public class CacheCotizacion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string CodigoMoneda { get; set; } = "";
    public DateTime Fecha { get; set; }
    public decimal CotizacionCompra { get; set; }
    public decimal CotizacionVenta { get; set; }
}

public class SyncMetadata
{
    [Key]
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AuthSession
{
    [Key]
    public int Id { get; set; } = 1;
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string Username { get; set; } = "";
    public string Rol { get; set; } = "";
}
