using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("clientes")]
public class Cliente
{
    [Key] [Column("id")] public int Id { get; set; }
    [Required] [Column("nombre")] public string Nombre { get; set; } = "";
    [Column("documento")] public string Documento { get; set; } = "";
    [Column("email")] public string Email { get; set; } = "";
    [Column("fecha_alta")] public DateTime FechaAlta { get; set; } = DateTime.Now;
}
