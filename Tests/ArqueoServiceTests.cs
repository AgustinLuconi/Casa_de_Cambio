using SistemaCambio.Models;
using SistemaCambio.Services;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para ArqueoService - ahora con DI e InMemory database.
    /// Gracias al refactor, podemos probar el flujo completo de arqueo.
    /// </summary>
    public class ArqueoServiceTests
    {
        private readonly IArqueoService _arqueoService;
        private readonly TestDbContextFactory _factory;

        public ArqueoServiceTests()
        {
            _factory = new TestDbContextFactory();
            var auditService = new AuditService(_factory);
            _arqueoService = new ArqueoService(_factory, auditService);

            // Seed datos de prueba
            using var db = _factory.CreateDbContext();
            db.Cuentas.Add(new Cuenta { Id = 1, Nombre = "Caja Pesos", Tipo = "Caja" });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 1, Moneda = "ARS", Saldo = 10000m });
            db.SaveChanges();
        }

        [Fact]
        public void RealizarArqueoCiego_CuentaNoExiste_DeberiaRetornarError()
        {
            var resultado = _arqueoService.RealizarArqueoCiego(
                cuentaId: 999999,
                moneda: "ARS",
                montoContado: 1000m
            );

            Assert.False(resultado.Exitoso);
            Assert.Contains("no encontrada", resultado.Mensaje);
        }

        // ============================================
        // NUEVO: Tests de arqueo completo (antes imposibles)
        // ============================================

        [Fact]
        public void RealizarArqueoCiego_SinDiferencia_DeberiaSerExitoso()
        {
            var resultado = _arqueoService.RealizarArqueoCiego(
                cuentaId: 1,
                moneda: "ARS",
                montoContado: 10000m  // Mismo que el saldo
            );

            Assert.True(resultado.Exitoso);
            Assert.Equal(0m, resultado.Diferencia);
        }

        [Fact]
        public void RealizarArqueoCiego_ConSobrante_DeberiaAjustarSaldo()
        {
            var resultado = _arqueoService.RealizarArqueoCiego(
                cuentaId: 1,
                moneda: "ARS",
                montoContado: 10500m  // 500 de sobrante
            );

            Assert.True(resultado.Exitoso);
            Assert.Equal(500m, resultado.Diferencia);

            // Verificar que el saldo se actualizó
            using var db = _factory.CreateDbContext();
            var saldo = db.SaldosCuenta.First(s => s.CuentaId == 1 && s.Moneda == "ARS");
            Assert.Equal(10500m, saldo.Saldo);
        }

        [Fact]
        public void RealizarArqueoCiego_ConFaltante_DeberiaAjustarSaldo()
        {
            var resultado = _arqueoService.RealizarArqueoCiego(
                cuentaId: 1,
                moneda: "ARS",
                montoContado: 9500m  // 500 de faltante
            );

            Assert.True(resultado.Exitoso);
            Assert.Equal(-500m, resultado.Diferencia);

            // Verificar que el saldo se actualizó
            using var db = _factory.CreateDbContext();
            var saldo = db.SaldosCuenta.First(s => s.CuentaId == 1 && s.Moneda == "ARS");
            Assert.Equal(9500m, saldo.Saldo);
        }
    }
}
