using SistemaCambio.Models;
using SistemaCambio.Services;
using SistemaCambio.Services.Validators;
using Xunit;
using System.Linq;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para OperacionService - ahora usa DI con InMemory database.
    /// Gracias al refactor de DI, podemos inyectar un DbContext en memoria.
    /// </summary>
    public class OperacionServiceTests
    {
        private readonly IOperacionService _operacionService;
        private readonly TestDbContextFactory _factory;

        public OperacionServiceTests()
        {
            _factory = new TestDbContextFactory();
            var auditService = new AuditService(_factory);
            var cierreCajaService = new CierreCajaService(_factory, auditService);
            var validator = new OperacionValidator(_factory);
            _operacionService = new OperacionService(_factory, auditService, cierreCajaService, validator);

            // Seed datos de prueba
            using var db = _factory.CreateDbContext();
            db.Cuentas.Add(new Cuenta { Id = 1, Nombre = "Caja Pesos", Tipo = "Caja" });
            db.Cuentas.Add(new Cuenta { Id = 2, Nombre = "Caja USD", Tipo = "Caja" });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 1, Moneda = "ARS", Saldo = 1000000m });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 2, Moneda = "USD", Saldo = 5000m });
            db.SaveChanges();
        }

        [Fact]
        public void GuardarOperacion_CuentaOrigenNoExiste_DeberiaRetornarError()
        {
            var resultado = _operacionService.GuardarOperacion(
                tipo: "Compra",
                cuentaOrigenId: 999999,
                cuentaDestinoId: 1,
                monedaOrigen: "USD",
                monedaDestino: "ARS",
                montoOrigen: 100m,
                montoDestino: 0.1m,
                cotizacion: 1000m
            );

            Assert.False(resultado.Exitoso);
            Assert.Contains("no existe", resultado.Mensaje);
        }

        [Fact]
        public void GuardarOperacion_CuentaDestinoNoExiste_DeberiaRetornarError()
        {
            var resultado = _operacionService.GuardarOperacion(
                tipo: "Compra",
                cuentaOrigenId: 1,
                cuentaDestinoId: 999999,
                monedaOrigen: "USD",
                monedaDestino: "ARS",
                montoOrigen: 100m,
                montoDestino: 0.1m,
                cotizacion: 1000m
            );

            Assert.False(resultado.Exitoso);
            Assert.Contains("no existe", resultado.Mensaje);
        }

        [Fact]
        public void GuardarCreditoDebito_CuentaNoExiste_DeberiaRetornarError()
        {
            var resultado = _operacionService.GuardarCreditoDebito(
                cuentaCreditoId: 999999,
                cuentaDebitoId: 1,
                monedaCredito: "ARS",
                monedaDebito: "ARS",
                montoCredito: 100m,
                montoDebito: 100m,
                cotizacion: 1m
            );

            Assert.False(resultado.Exitoso);
            Assert.Contains("no existe", resultado.Mensaje);
        }

        // ============================================
        // NUEVO: Tests que antes eran imposibles sin DI
        // ============================================

        [Fact]
        public void GuardarOperacion_Exitosa_DeberiaActualizarSaldos()
        {
            var resultado = _operacionService.GuardarOperacion(
                tipo: "Compra",
                cuentaOrigenId: 1,
                cuentaDestinoId: 2,
                monedaOrigen: "ARS",
                monedaDestino: "USD",
                montoOrigen: 1000m,
                montoDestino: 1m,
                cotizacion: 1000m
            );

            Assert.True(resultado.Exitoso);
            Assert.NotNull(resultado.OperacionId);

            // Verificar saldos actualizados
            using var db = _factory.CreateDbContext();
            var saldoPesos = db.SaldosCuenta.First(s => s.CuentaId == 1 && s.Moneda == "ARS");
            var saldoUSD = db.SaldosCuenta.First(s => s.CuentaId == 2 && s.Moneda == "USD");

            Assert.Equal(999000m, saldoPesos.Saldo);  // 1,000,000 - 1,000
            Assert.Equal(5001m, saldoUSD.Saldo);       // 5,000 + 1
        }

        [Fact]
        public void GuardarOperacion_SaldoInsuficiente_DeberiaRetornarError()
        {
            var resultado = _operacionService.GuardarOperacion(
                tipo: "Compra",
                cuentaOrigenId: 1,
                cuentaDestinoId: 2,
                monedaOrigen: "ARS",
                monedaDestino: "USD",
                montoOrigen: 9999999m,  // Más que el saldo disponible
                montoDestino: 1m,
                cotizacion: 1m
            );

            Assert.False(resultado.Exitoso);
            Assert.Contains("insuficiente", resultado.Mensaje);
        }
    }
}
