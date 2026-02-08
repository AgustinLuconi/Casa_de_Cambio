using SistemaCambio.Services;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para PPPService - el servicio de Precio Promedio Ponderado.
    /// 
    /// PPP = Costo Promedio Ponderado
    /// Es el costo promedio al que compraste una divisa.
    /// Sirve para saber si vendes con ganancia o pérdida.
    /// 
    /// Ejemplo:
    /// - Compras 100 USD a $900 = Costo: $90,000
    /// - Compras 100 USD a $1100 = Costo: $110,000
    /// - Total: 200 USD, Costo total: $200,000
    /// - PPP = $200,000 / 200 = $1,000 por USD
    /// </summary>
    public class PPPServiceTests
    {
        // ============================================
        // TESTS: Validación de Venta
        // ============================================

        [Fact]
        public void ValidarVenta_SinHistorialDeCompras_SinAdvertencia()
        {
            // Si nunca compraste esa moneda, no hay PPP que calcular
            var resultado = PPPService.ValidarVenta("XYZ", 1000m); // Moneda inexistente
            
            // No debería haber advertencia porque no hay historial
            Assert.True(string.IsNullOrEmpty(resultado.Mensaje) || !resultado.Mensaje.Contains("⚠️"));
        }

        [Fact]
        public void ValidarVenta_RetornaPPPActual()
        {
            // El método debería retornar información del PPP
            var resultado = PPPService.ValidarVenta("USD", 1000m);
            
            // PPP debería ser >= 0 (puede ser 0 si no hay tenencia)
            Assert.True(resultado.PPP >= 0);
        }

        // ============================================
        // TESTS: Obtener PPP
        // ============================================

        [Fact]
        public void ObtenerPPP_MonedaSinHistorial_RetornaCero()
        {
            decimal ppp = PPPService.ObtenerPPP("MONEDA_QUE_NO_EXISTE");
            Assert.Equal(0m, ppp);
        }

        // ============================================
        // NOTA EDUCATIVA: Tests de Integración
        // ============================================
        
        // Los tests que modifican la base de datos (RegistrarCompra, RegistrarVenta)
        // requieren un setup más complejo con una base de datos de prueba.
        // Por ahora nos enfocamos en tests unitarios puros.
        //
        // Para tests de integración completos, necesitaríamos:
        // 1. Refactorizar PPPService para inyectar el DbContext
        // 2. Usar una base de datos en memoria para cada test
        // 3. Limpiar la DB después de cada test
    }
}
