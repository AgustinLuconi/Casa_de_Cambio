namespace CasaCambio.Shared.DTOs;

public class ClienteDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime FechaAlta { get; set; }
}
