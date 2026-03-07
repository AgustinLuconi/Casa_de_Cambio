namespace SistemaCambio.Services
{
    public interface IOperacionService
    {
        OperacionResult GuardarOperacion(
            string tipo,
            int cuentaOrigenId,
            int cuentaDestinoId,
            string monedaOrigen,
            string monedaDestino,
            decimal montoOrigen,
            decimal montoDestino,
            decimal cotizacion,
            int? clienteId = null,
            string observaciones = "");

        OperacionResult GuardarOperacionInterbancaria(
            string tipo,
            int cuentaOrigenId,
            int cuentaDestinoId,
            string monedaOrigen,
            string monedaDestino,
            decimal montoOrigen,
            decimal montoDestino,
            decimal cotizacion,
            string observaciones = "");

        OperacionResult GuardarCreditoDebito(
            int cuentaCreditoId,
            int cuentaDebitoId,
            string monedaCredito,
            string monedaDebito,
            decimal montoCredito,
            decimal montoDebito,
            decimal cotizacion,
            int? clienteId = null,
            string observaciones = "");
    }
}
