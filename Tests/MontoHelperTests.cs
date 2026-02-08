using SistemaCambio.Services;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para MontoHelper - el helper que parsea montos en diferentes formatos.
    /// 
    /// CÓMO FUNCIONAN LOS TESTS:
    /// - Cada método con [Fact] es un test individual
    /// - Assert.Equal(esperado, resultado) verifica que el resultado sea igual al esperado
    /// - Si falla, te dice exactamente qué esperabas vs qué obtuviste
    /// </summary>
    public class MontoHelperTests
    {
        // ============================================
        // TESTS: Formato Argentino (10.000.000,50)
        // Puntos = separador de miles
        // Coma = separador decimal
        // ============================================

        [Fact]
        public void Parsear_FormatoArgentino_ConDecimales()
        {
            // Arrange (Preparar)
            string input = "1.234.567,89";
            
            // Act (Ejecutar)
            decimal resultado = MontoHelper.Parsear(input);
            
            // Assert (Verificar)
            Assert.Equal(1234567.89m, resultado);
        }

        [Fact]
        public void Parsear_FormatoArgentino_SinDecimales()
        {
            string input = "10.000.000";
            decimal resultado = MontoHelper.Parsear(input);
            Assert.Equal(10000000m, resultado);
        }

        [Fact]
        public void Parsear_FormatoArgentino_Millones()
        {
            // Este es el caso que causó el bug original
            string input = "10.000.000,00";
            decimal resultado = MontoHelper.Parsear(input);
            Assert.Equal(10000000m, resultado);
        }

        // ============================================
        // TESTS: Formato Americano (10,000,000.50)
        // Comas = separador de miles
        // Punto = separador decimal
        // ============================================

        [Fact]
        public void Parsear_FormatoAmericano_ConDecimales()
        {
            string input = "1,234,567.89";
            decimal resultado = MontoHelper.Parsear(input);
            Assert.Equal(1234567.89m, resultado);
        }

        [Fact]
        public void Parsear_FormatoAmericano_SinDecimales()
        {
            string input = "10,000,000";
            decimal resultado = MontoHelper.Parsear(input);
            Assert.Equal(10000000m, resultado);
        }

        // ============================================
        // TESTS: Números simples (sin separadores)
        // ============================================

        [Fact]
        public void Parsear_NumeroSimple_Entero()
        {
            string input = "12345";
            decimal resultado = MontoHelper.Parsear(input);
            Assert.Equal(12345m, resultado);
        }

        [Fact]
        public void Parsear_NumeroSimple_ConPuntoDecimal()
        {
            string input = "1234.56";
            decimal resultado = MontoHelper.Parsear(input);
            Assert.Equal(1234.56m, resultado);
        }

        [Fact]
        public void Parsear_NumeroSimple_ConComaDecimal()
        {
            string input = "1234,56";
            decimal resultado = MontoHelper.Parsear(input);
            Assert.Equal(1234.56m, resultado);
        }

        // ============================================
        // TESTS: Casos edge (extremos/especiales)
        // ============================================

        [Fact]
        public void Parsear_TextoVacio_RetornaCero()
        {
            Assert.Equal(0m, MontoHelper.Parsear(""));
            Assert.Equal(0m, MontoHelper.Parsear("   "));
            Assert.Equal(0m, MontoHelper.Parsear(null));
        }

        [Fact]
        public void Parsear_TextoInvalido_RetornaCero()
        {
            Assert.Equal(0m, MontoHelper.Parsear("abc"));
            Assert.Equal(0m, MontoHelper.Parsear("$1000"));
        }

        [Fact]
        public void Parsear_CeroExplicito()
        {
            Assert.Equal(0m, MontoHelper.Parsear("0"));
            Assert.Equal(0m, MontoHelper.Parsear("0,00"));
            Assert.Equal(0m, MontoHelper.Parsear("0.00"));
        }

        // ============================================
        // TESTS: Casos de uso real del sistema
        // ============================================

        [Fact]
        public void Parsear_MontoCompraUSD_CasoReal()
        {
            // Usuario quiere comprar 10,000 USD
            string montoDestino = "10000";
            string cotizacion = "1000";
            
            decimal montoUSD = MontoHelper.Parsear(montoDestino);
            decimal cotiz = MontoHelper.Parsear(cotizacion);
            decimal totalPesos = montoUSD * cotiz;
            
            Assert.Equal(10000m, montoUSD);
            Assert.Equal(1000m, cotiz);
            Assert.Equal(10000000m, totalPesos); // 10 millones de pesos
        }

        [Fact]
        public void Parsear_SaldoCuenta_FormatoDisplay()
        {
            // El sistema muestra saldos con formato: 2.500.000,00
            string saldoMostrado = "2.500.000,00";
            decimal saldo = MontoHelper.Parsear(saldoMostrado);
            Assert.Equal(2500000m, saldo);
        }
    }
}
