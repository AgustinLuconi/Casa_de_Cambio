namespace SistemaCambio.ViewModels.Models
{
    public class FiltroItem<T>
    {
        public string Nombre { get; set; } = "";
        public T Valor { get; set; } = default!;
        public override string ToString() => Nombre;
    }
}
