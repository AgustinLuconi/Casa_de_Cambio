namespace CasaCambio.Shared.DTOs;

public class AuditLogDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string UsuarioNombre { get; set; } = "";
    public string Accion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public int EntidadId { get; set; }
    public string? ValoresAnteriores { get; set; }
    public string? ValoresNuevos { get; set; }
}
