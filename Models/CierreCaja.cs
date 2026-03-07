using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.Models
{
    /// <summary>
    /// Representa el cierre diario de caja.
    /// 
    /// PROPÓSITO:
    /// - Agrupa todas las operaciones del día
    /// - Registra saldos finales de cada moneda
    /// - Permite bloquear operaciones en días cerrados
    /// - Genera historial para auditoría
    /// 
    /// Una vez cerrado definitivamente (Cerrado = true), 
    /// NO se pueden agregar más operaciones a ese día.
    /// </summary>
    [Table("cierres_caja")]
    public class CierreCaja
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Fecha del día que se está cerrando (solo fecha, sin hora)
        /// </summary>
        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        /// <summary>
        /// Momento exacto en que se realizó el cierre
        /// </summary>
        [Column("fecha_cierre")]
        public DateTime FechaCierre { get; set; } = DateTime.Now;

        /// <summary>
        /// Usuario que realizó el cierre
        /// </summary>
        [Column("usuario")]
        public string Usuario { get; set; } = "Admin";

        // =============================================
        // RESUMEN DE COMPRAS (Ingreso de divisa extranjera)
        // =============================================
        
        [Column("cantidad_compras")]
        public int CantidadCompras { get; set; }

        [Column("total_compras_usd")]
        public decimal TotalComprasUSD { get; set; }

        [Column("total_compras_ars")]
        public decimal TotalComprasARS { get; set; }

        // =============================================
        // RESUMEN DE VENTAS (Salida de divisa extranjera)
        // =============================================

        [Column("cantidad_ventas")]
        public int CantidadVentas { get; set; }

        [Column("total_ventas_usd")]
        public decimal TotalVentasUSD { get; set; }

        [Column("total_ventas_ars")]
        public decimal TotalVentasARS { get; set; }

        // =============================================
        // SALDOS FINALES DE CAJA
        // =============================================

        [Column("saldo_caja_ars")]
        public decimal SaldoCajaARS { get; set; }

        [Column("saldo_caja_usd")]
        public decimal SaldoCajaUSD { get; set; }

        [Column("saldo_caja_eur")]
        public decimal SaldoCajaEUR { get; set; }

        // =============================================
        // CONTROL Y AUDITORÍA
        // =============================================

        /// <summary>
        /// Suma de todas las diferencias de arqueos del día
        /// </summary>
        [Column("total_diferencias")]
        public decimal TotalDiferencias { get; set; }

        [Column("observaciones")]
        public string Observaciones { get; set; } = "";

        /// <summary>
        /// Si es TRUE, el día está cerrado y NO se pueden agregar operaciones
        /// </summary>
        [Column("cerrado")]
        public bool Cerrado { get; set; } = false;
    }
}
