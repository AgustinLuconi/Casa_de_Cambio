namespace SistemaCambio.ViewModels.Models
{
    /// <summary>Fila editable del límite de deuda global para una divisa.</summary>
    public class LimiteDivisaModel
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string LimiteTexto { get; set; } = "0";
    }
}
