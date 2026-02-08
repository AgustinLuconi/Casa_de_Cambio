using SistemaCambio.Services;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para AuditService - el servicio de auditoría.
    /// 
    /// AUDITORÍA = Registro de todas las acciones importantes del sistema.
    /// Sirve para:
    /// - Saber quién hizo qué y cuándo
    /// - Detectar fraudes o errores
    /// - Cumplir con regulaciones financieras
    /// 
    /// IMPORTANTE: El AuditService está diseñado para NUNCA fallar.
    /// Si hay un error al guardar el log, falla silenciosamente para no
    /// interrumpir la operación principal.
    /// </summary>
    public class AuditServiceTests
    {
        // ============================================
        // TESTS: El servicio no debe fallar nunca
        // ============================================

        [Fact]
        public void Registrar_DeberiaEjecutarseSinExcepciones()
        {
            // El servicio debería ejecutarse sin tirar excepciones
            // incluso si los datos no son perfectos
            var exception = Record.Exception(() =>
            {
                AuditService.Registrar(
                    accion: "TEST",
                    entidad: "Prueba",
                    entidadId: 1,
                    datosNuevos: new { mensaje = "Esto es un test" }
                );
            });

            Assert.Null(exception);
        }

        [Fact]
        public void Registrar_ConDatosNulos_NoDeberiaFallar()
        {
            // Probar con datos mínimos - no debería fallar
            var exception = Record.Exception(() =>
            {
                AuditService.Registrar("CREATE", "Test", 1);
            });

            Assert.Null(exception);
        }

        [Fact]
        public void RegistrarCambioCotizacion_NoDeberiaFallar()
        {
            var exception = Record.Exception(() =>
            {
                AuditService.RegistrarCambioCotizacion(
                    monedaId: 1,
                    cotizacionAnterior: 1000m,
                    cotizacionNueva: 1050m
                );
            });

            Assert.Null(exception);
        }

        [Fact]
        public void RegistrarEliminacion_NoDeberiaFallar()
        {
            var exception = Record.Exception(() =>
            {
                AuditService.RegistrarEliminacion(
                    entidad: "Operacion",
                    entidadId: 123,
                    datosEliminados: new { tipo = "Compra", monto = 1000 }
                );
            });

            Assert.Null(exception);
        }
    }
}
