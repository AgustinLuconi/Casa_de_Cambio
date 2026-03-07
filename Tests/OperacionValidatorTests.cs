using SistemaCambio.Models;
using SistemaCambio.Services.Validators;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para OperacionValidator — valida operaciones ANTES de guardarlas.
    /// </summary>
    public class OperacionValidatorTests
    {
        private readonly OperacionValidator _validator;
        private readonly TestDbContextFactory _factory;

        public OperacionValidatorTests()
        {
            _factory = new TestDbContextFactory();
            _validator = new OperacionValidator(_factory);

            // Seed
            using var db = _factory.CreateDbContext();
            db.Cuentas.Add(new Cuenta { Id = 1, Nombre = "Caja Pesos", Tipo = "Caja" });
            db.Cuentas.Add(new Cuenta { Id = 2, Nombre = "Caja USD", Tipo = "Caja" });
            db.Cuentas.Add(new Cuenta { Id = 3, Nombre = "Caja EUR", Tipo = "Caja" });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 1, Moneda = "ARS", Saldo = 500000m });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 2, Moneda = "USD", Saldo = 5000m });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 3, Moneda = "EUR", Saldo = 2000m });
            db.SaveChanges();
        }

        [Fact]
        public void CuentaNoExiste_DeberiaRetornarError()
        {
            var result = _validator.ValidarOperacion("Venta", 999, 1, "USD", "ARS", 100, 95000, 950);
            Assert.True(result.HasErrors);
            Assert.Contains(result.Errors, e => e.Message.Contains("no existe"));
        }

        [Fact]
        public void MontoCero_DeberiaRetornarError()
        {
            var result = _validator.ValidarOperacion("Venta", 2, 1, "USD", "ARS", 0, 0, 950);
            Assert.True(result.HasErrors);
            Assert.Contains(result.Errors, e => e.Message.Contains("mayor a cero"));
        }

        [Fact]
        public void CotizacionNegativa_DeberiaRetornarError()
        {
            var result = _validator.ValidarOperacion("Venta", 2, 1, "USD", "ARS", 100, 95000, -1);
            Assert.True(result.HasErrors);
            Assert.Contains(result.Errors, e => e.Message.Contains("Cotización"));
        }

        [Fact]
        public void CotizacionMuyBaja_DeberiaRetornarWarning()
        {
            var result = _validator.ValidarOperacion("Venta", 2, 1, "USD", "ARS", 100, 50, 0.5m);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("muy baja"));
        }

        [Fact]
        public void CotizacionMuyAlta_DeberiaRetornarWarning()
        {
            var result = _validator.ValidarOperacion("Venta", 2, 1, "USD", "ARS", 100, 1500000, 15000);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("muy alta"));
        }

        [Fact]
        public void MismaCuenta_DeberiaRetornarError()
        {
            var result = _validator.ValidarOperacion("Venta", 1, 1, "ARS", "ARS", 100, 100, 1);
            Assert.True(result.HasErrors);
            Assert.Contains(result.Errors, e => e.Message.Contains("misma moneda en la misma cuenta"));
        }

        [Fact]
        public void SaldoInsuficiente_DeberiaRetornarError()
        {
            // Caja USD tiene 5000, querer vender 10000
            var result = _validator.ValidarOperacion("Venta", 2, 1, "USD", "ARS", 10000, 9500000, 950);
            Assert.True(result.HasErrors);
            Assert.Contains(result.Errors, e => e.Message.Contains("Saldo insuficiente"));
        }

        [Fact]
        public void SaldoQuedaBajo_DeberiaRetornarWarning()
        {
            // Caja USD tiene 5000, vender 4950 deja 50 USD (< 100)
            var result = _validator.ValidarOperacion("Venta", 2, 1, "USD", "ARS", 4950, 4702500, 950);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("Saldo quedará muy bajo"));
        }

        [Fact]
        public void CompraConCuentaOrigenDivisa_DeberiaWarning()
        {
            // En compra, origen debería ser ARS. Aquí es USD.
            var result = _validator.ValidarOperacion("Compra", 2, 1, "USD", "ARS", 100, 95000, 950);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("Posible error de moneda"));
        }

        [Fact]
        public void VentaConDestinoNoPesos_DeberiaWarning()
        {
            // En venta, destino debería ser ARS. Aquí es EUR.
            var result = _validator.ValidarOperacion("Venta", 2, 3, "USD", "EUR", 100, 100, 1);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("Posible error de moneda"));
        }

        [Fact]
        public void CoherenciaMatematica_DiferenciaGrande_DeberiaWarning()
        {
            // Vender 1000 USD a cotización 950 → esperado $950,000 ARS
            // Pero ingresa $95,000 (error de tipeo, le faltó un 0)
            var result = _validator.ValidarOperacion("Venta", 2, 1, "USD", "ARS", 1000, 95000, 950);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("ERROR DE CÁLCULO"));
        }

        [Fact]
        public void OperacionValida_SinErroresNiWarnings()
        {
            // Venta de 100 USD a 950 → $95,000 ARS. Todo correcto.
            var result = _validator.ValidarOperacion("Venta", 2, 1, "USD", "ARS", 100, 95000, 950);
            Assert.False(result.HasErrors);
            Assert.False(result.HasWarnings);
        }

        // ─── CreditoDebito ───────────────────────────────────────

        [Fact]
        public void CreditoDebito_MismaCuenta_DeberiaRetornarError()
        {
            var result = _validator.ValidarCreditoDebito(1, 1, "ARS", "ARS", 100, 100);
            Assert.True(result.HasErrors);
            Assert.Contains(result.Errors, e => e.Message.Contains("misma cuent"));
        }

        [Fact]
        public void CreditoDebito_MonedasDiferentes_DeberiaWarning()
        {
            var result = _validator.ValidarCreditoDebito(2, 1, "USD", "ARS", 100, 95000);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("Monedas diferentes"));
        }
    }
}
