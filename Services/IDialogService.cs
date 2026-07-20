using System.Threading.Tasks;

namespace SistemaCambio.Services
{
    public interface IDialogService
    {
        Task<bool> ConfirmarAsync(string titulo, string mensaje,
            string textoBtnConfirmar = "Continuar", string textoBtnCancelar = "Cancelar",
            bool destructivo = false);

        Task MensajeAsync(string titulo, string mensaje);
    }
}
