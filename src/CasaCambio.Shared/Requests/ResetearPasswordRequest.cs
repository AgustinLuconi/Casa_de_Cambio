namespace CasaCambio.Shared.Requests;

public class ResetearPasswordRequest
{
    public string Token { get; set; } = "";
    public string NuevaPassword { get; set; } = "";
    public string ConfirmarPassword { get; set; } = "";
}
