using SistemaCambio.Models;

namespace SistemaCambio.Services
{
    public interface ICierreCajaService
    {
        CierreResult GenerarCierre(string observaciones = "");
        CierreResult CerrarDefinitivo(int cierreId);
        bool HayDiaCerrado();
        CierreCaja? ObtenerCierreDeHoy();
        CierreCaja? ObtenerUltimoCierre();
    }
}
