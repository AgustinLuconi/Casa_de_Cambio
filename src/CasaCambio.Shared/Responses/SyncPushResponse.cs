namespace CasaCambio.Shared.Responses;

public class SyncPushResponse
{
    public List<SyncResultItem> Resultados { get; set; } = new();
}

public class SyncResultItem
{
    public string LocalId { get; set; } = "";
    public int? ServerOperacionId { get; set; }
    public bool Exitoso { get; set; }
    public string? Mensaje { get; set; }
}
