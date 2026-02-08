using SistemaCambio.Services;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para ArqueoService - el servicio de arqueo ciego de caja.
    /// 
    /// ARQUEO CIEGO = El operador cuenta el dinero físico SIN ver el saldo del sistema.
    /// Después de contar, se compara con el sistema y se genera un asiento de ajuste automático.
    /// 
    /// Esto previene:
    /// - Fraude (porque el operador no sabe cuánto debería haber)
    /// - Errores humanos (el sistema ajusta automáticamente las diferencias)
    /// </summary>
    public class ArqueoServiceTests
    {
        // ============================================
        // TESTS: Validación de entrada
        // ============================================

        [Fact]
        public void RealizarArqueoCiego_CuentaNoExiste_DeberiaRetornarError()
        {
            // Cuenta 999999 definitivamente no existe
            var resultado = ArqueoService.RealizarArqueoCiego(
                cuentaId: 999999,
                montoContado: 1000m
            );

            Assert.False(resultado.Exitoso);
            Assert.Contains("no encontrada", resultado.Mensaje);
        }

        // ============================================
        // NOTA EDUCATIVA: Tests de Integración
        // ============================================
        
        // Para probar el arqueo completo (con diferencias y asientos de ajuste)
        // necesitaríamos una base de datos de prueba aislada.
        // 
        // El flujo completo sería:
        // 1. Crear cuenta de prueba con saldo conocido (ej: 10000)
        // 2. Hacer arqueo con monto diferente (ej: 10500 = sobrante de 500)
        // 3. Verificar que se creó el asiento de ajuste
        // 4. Verificar que el saldo de la cuenta ahora es 10500
        // 5. Verificar que existe el registro en cta "Diferencias de Caja"
    }
}
