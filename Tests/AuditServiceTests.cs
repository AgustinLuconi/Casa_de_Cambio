using SistemaCambio.Services;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para AuditService - ahora usa DI con InMemory database.
    /// 
    /// IMPORTANTE: El AuditService está diseñado para NUNCA fallar.
    /// Si hay un error al guardar el log, falla silenciosamente.
    /// </summary>
    public class AuditServiceTests
    {
        private readonly IAuditService _auditService;

        public AuditServiceTests()
        {
            var factory = new TestDbContextFactory();
            _auditService = new AuditService(factory);
        }

        [Fact]
        public void Registrar_DeberiaEjecutarseSinExcepciones()
        {
            var exception = Record.Exception(() =>
            {
                _auditService.Registrar(
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
            var exception = Record.Exception(() =>
            {
                _auditService.Registrar("CREATE", "Test", 1);
            });

            Assert.Null(exception);
        }

        [Fact]
        public void RegistrarCambioCotizacion_NoDeberiaFallar()
        {
            var exception = Record.Exception(() =>
            {
                _auditService.RegistrarCambioCotizacion(
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
                _auditService.RegistrarEliminacion(
                    entidad: "Operacion",
                    entidadId: 123,
                    datosEliminados: new { tipo = "Compra", monto = 1000 }
                );
            });

            Assert.Null(exception);
        }
    }
}
