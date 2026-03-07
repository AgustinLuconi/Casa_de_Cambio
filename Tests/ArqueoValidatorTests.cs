using SistemaCambio.Models;
using SistemaCambio.Services.Validators;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para ArqueoValidator — valida diferencias en conteos de caja.
    /// </summary>
    public class ArqueoValidatorTests
    {
        private readonly ArqueoValidator _validator = new();

        [Fact]
        public void MontoNegativo_DeberiaRetornarError()
        {
            var result = _validator.ValidarArqueo("Caja", "ARS", 1000, -100);
            Assert.True(result.HasErrors);
        }

        [Fact]
        public void SinDiferencia_DeberiaRetornarInfo()
        {
            var result = _validator.ValidarArqueo("Caja", "ARS", 1000, 1000);
            Assert.False(result.HasErrors);
            Assert.False(result.HasWarnings);
            Assert.Contains(result.Messages, m => m.Message.Contains("cuadra perfectamente"));
        }

        [Fact]
        public void DiferenciaMenor_50pesos_DeberiaSerInfo()
        {
            var result = _validator.ValidarArqueo("Caja", "ARS", 10000, 10030);
            Assert.False(result.HasWarnings);
            Assert.False(result.HasErrors);
        }

        [Fact]
        public void DiferenciaMedia_200pesos_DeberiaSerWarning()
        {
            var result = _validator.ValidarArqueo("Caja", "ARS", 10000, 10200);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("Diferencia detectada"));
        }

        [Fact]
        public void DiferenciaGrande_1500pesos_DeberiaSerWarningFuerte()
        {
            var result = _validator.ValidarArqueo("Caja", "ARS", 50000, 48500);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("SIGNIFICATIVA"));
        }

        [Fact]
        public void DiferenciaMuyGrande_5000pesos_DeberiaSerError()
        {
            var result = _validator.ValidarArqueo("Caja", "ARS", 50000, 45000);
            Assert.True(result.HasErrors);
            Assert.Contains(result.Errors, e => e.Message.Contains("MUY GRANDE"));
        }
    }
}
