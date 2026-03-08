namespace CasaCambio.Shared.Responses;

public class ApiErrorResponse
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
    public string? Details { get; set; }
}
