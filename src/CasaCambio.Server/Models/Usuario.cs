using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaCambio.Server.Models;

[Table("usuarios")]
public class Usuario
{
    [Key] [Column("id")] public int Id { get; set; }
    [Required] [Column("username")] [MaxLength(150)] public string Username { get; set; } = "";
    [Required] [Column("password_hash")] [MaxLength(256)] public string PasswordHash { get; set; } = "";
    [Required] [Column("nombre_completo")] [MaxLength(100)] public string NombreCompleto { get; set; } = "";
    [Column("rol")] [MaxLength(20)] public string Rol { get; set; } = "Admin";
    [Column("activo")] public bool Activo { get; set; } = true;
    [Column("fecha_creacion")] public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    [Column("email")] [MaxLength(150)] public string Email { get; set; } = "";
    [Column("email_confirmado")] public bool EmailConfirmado { get; set; } = false;
    [Column("token_confirmacion")] [MaxLength(256)] public string? TokenConfirmacion { get; set; }
    [Column("token_recuperacion")] [MaxLength(256)] public string? TokenRecuperacion { get; set; }
    [Column("token_expiracion")] public DateTime? TokenExpiracion { get; set; }
    [Column("token_confirmacion_expiry")] public DateTime? TokenConfirmacionExpiry { get; set; }
    [Column("refresh_token")] [MaxLength(256)] public string? RefreshToken { get; set; }
    [Column("refresh_token_expiry")] public DateTime? RefreshTokenExpiry { get; set; }
}
