using SistemaCambio.Services;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para OperacionService - el servicio que maneja operaciones de cambio.
    /// 
    /// NOTA: OperacionService usa su propio DbContext internamente,
    /// por lo que estos tests verifican casos de error que no dependen
    /// de datos específicos en la base de datos.
    /// 
    /// Para tests más completos, necesitaríamos refactorizar el servicio
    /// para aceptar inyección de dependencias (Dependency Injection).
    /// </summary>
    public class OperacionServiceTests
    {
        // ============================================
        // TESTS: Cuentas inexistentes
        // ============================================

        [Fact]
        public void GuardarOperacion_CuentaOrigenNoExiste_DeberiaRetornarError()
        {
            // Cuenta 999999 definitivamente no existe
            var resultado = OperacionService.GuardarOperacion(
                tipo: "Compra",
                cuentaOrigenId: 999999,
                cuentaDestinoId: 1,
                montoOrigen: 100m,
                montoDestino: 0.1m,
                cotizacion: 1000m
            );

            Assert.False(resultado.Exitoso);
            Assert.Contains("no encontrada", resultado.Mensaje);
        }

        [Fact]
        public void GuardarOperacion_CuentaDestinoNoExiste_DeberiaRetornarError()
        {
            // Si la cuenta origen existe pero destino no
            var resultado = OperacionService.GuardarOperacion(
                tipo: "Compra",
                cuentaOrigenId: 1,
                cuentaDestinoId: 999999,
                montoOrigen: 100m,
                montoDestino: 0.1m,
                cotizacion: 1000m
            );

            Assert.False(resultado.Exitoso);
            Assert.Contains("no encontrada", resultado.Mensaje);
        }

        // ============================================
        // TEST: Crédito/Débito con cuentas inexistentes
        // ============================================

        [Fact]
        public void GuardarCreditoDebito_CuentaNoExiste_DeberiaRetornarError()
        {
            var resultado = OperacionService.GuardarCreditoDebito(
                cuentaCreditoId: 999999,
                cuentaDebitoId: 1,
                montoCredito: 100m,
                montoDebito: 100m,
                cotizacion: 1m
            );

            Assert.False(resultado.Exitoso);
            Assert.Contains("no encontrada", resultado.Mensaje);
        }

        // ============================================
        // NOTA EDUCATIVA
        // ============================================
        
        // Para probar completamente las operaciones exitosas y las
        // validaciones de saldo, necesitamos controlar la base de datos.
        // 
        // Dos opciones para el futuro:
        // 1. Inyección de dependencias (DI) - pasar el DbContext como parámetro
        // 2. Tests de integración con DB de prueba separada
        //
        // Por ahora, los tests de MontoHelper son los más útiles porque
        // es una función pura sin dependencias externas.
    }
}
