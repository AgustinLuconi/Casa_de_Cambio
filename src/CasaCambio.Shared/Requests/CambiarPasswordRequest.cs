namespace CasaCambio.Shared.Requests;

public class CambiarPasswordRequest
{
    public string PasswordActual { get; set; } = "";
    public string NuevaPassword { get; set; } = "";
    public string ConfirmarPassword { get; set; } = "";
}
