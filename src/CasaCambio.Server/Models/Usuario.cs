using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("usuarios")]
public class Usuario
{
    [Key] [Column("id")] public int Id { get; set; }
    [Required] [Column("username")] [MaxLength(50)] public string Username { get; set; } = "";
    [Required] [Column("password_hash")] [MaxLength(256)] public string PasswordHash { get; set; } = "";
    [Required] [Column("nombre_completo")] [MaxLength(100)] public string NombreCompleto { get; set; } = "";
    [Column("rol")] [MaxLength(20)] public string Rol { get; set; } = "Admin";
    [Column("activo")] public bool Activo { get; set; } = true;
    [Column("fecha_creacion")] public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
