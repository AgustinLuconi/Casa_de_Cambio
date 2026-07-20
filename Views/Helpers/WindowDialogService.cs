using System.Threading.Tasks;
using Avalonia.Controls;
using SistemaCambio.Services;

namespace SistemaCambio.Views.Helpers
{
    public sealed class WindowDialogService : IDialogService
    {
        private readonly Window _owner;

        public WindowDialogService(Window owner) => _owner = owner;

        public Task<bool> ConfirmarAsync(string titulo, string mensaje,
            string textoBtnConfirmar = "Continuar", string textoBtnCancelar = "Cancelar",
            bool destructivo = false)
            => DialogHelper.ConfirmarAsync(_owner, titulo, mensaje, textoBtnConfirmar, textoBtnCancelar, destructivo);

        public Task MensajeAsync(string titulo, string mensaje)
            => DialogHelper.MensajeAsync(_owner, titulo, mensaje);
    }
}
