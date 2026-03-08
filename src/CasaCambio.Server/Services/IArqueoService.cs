namespace CasaCambio.Server.Services;

public class ArqueoResult
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? ArqueoId { get; set; }
    public decimal Diferencia { get; set; }
    public static ArqueoResult Success(int id, decimal diferencia) => new() { Exitoso = true, ArqueoId = id, Diferencia = diferencia };
    public static ArqueoResult Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}

public interface IArqueoService
{
    ArqueoResult RealizarArqueoCiego(int cuentaId, string moneda, decimal montoContado, string observaciones = "");
}
