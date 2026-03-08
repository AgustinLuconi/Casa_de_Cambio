using CasaCambio.Server.Models;

namespace CasaCambio.Server.Services;

public class CierreResult
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = "";
    public int? CierreId { get; set; }
    public CierreCaja? Cierre { get; set; }
    public static CierreResult Success(CierreCaja cierre) => new() { Exitoso = true, CierreId = cierre.Id, Cierre = cierre };
    public static CierreResult Error(string msg) => new() { Exitoso = false, Mensaje = msg };
}

public interface ICierreCajaService
{
    CierreResult GenerarCierre(string observaciones = "");
    CierreResult CerrarDefinitivo(int cierreId);
    bool HayDiaCerrado();
    CierreCaja? ObtenerCierreDeHoy();
    CierreCaja? ObtenerUltimoCierre();
}
