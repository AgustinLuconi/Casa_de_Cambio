using System;

namespace SistemaCambio.ViewModels.Models
{
    public class MovimientoDetalle
    {
        public int Id { get; set; }
        public string CodigoOperacion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string TipoOperacion { get; set; } = "";
        public string CuentaNombre { get; set; } = "";
        public string Moneda { get; set; } = "";
        public decimal Debito { get; set; }
        public decimal Credito { get; set; }
        public string Observaciones { get; set; } = "";
    }
}
