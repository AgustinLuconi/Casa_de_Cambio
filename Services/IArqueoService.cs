namespace SistemaCambio.Services
{
    public interface IArqueoService
    {
        ArqueoResult RealizarArqueoCiego(int cuentaId, string moneda, decimal montoContado, string observaciones = "");
    }
}
