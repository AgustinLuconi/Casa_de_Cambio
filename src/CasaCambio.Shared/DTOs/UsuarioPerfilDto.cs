namespace CasaCambio.Shared.DTOs;

public class UsuarioPerfilDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public string Email { get; set; } = "";
    public string Rol { get; set; } = "";
    public bool EmailConfirmado { get; set; }
    public DateTime FechaCreacion { get; set; }
}
