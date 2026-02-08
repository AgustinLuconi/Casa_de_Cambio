using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.Models
{
    [Table("monedas")]
    public class Moneda
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("codigo")]
        [MaxLength(10)]
        public string Codigo { get; set; } = "";  // "USD", "EUR", "ARS"

        [Required]
        [Column("nombre")]
        [MaxLength(100)]
        public string Nombre { get; set; } = "";  // "Dólar USD", "Euro"

        [Column("activa")]
        public bool Activa { get; set; } = true;
    }
}
