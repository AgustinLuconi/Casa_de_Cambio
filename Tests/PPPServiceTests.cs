using SistemaCambio.Models;
using SistemaCambio.Services;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests para PPPService - ahora con DI e InMemory database.
    /// Gracias al refactor, podemos probar compras y ventas aisladas.
    /// </summary>
    public class PPPServiceTests
    {
        private readonly IPPPService _pppService;
        private readonly TestDbContextFactory _factory;

        public PPPServiceTests()
        {
            _factory = new TestDbContextFactory();
            _pppService = new PPPService(_factory);

            // Seed monedas de prueba
            using var db = _factory.CreateDbContext();
            db.Monedas.Add(new Moneda { Id = 1, Codigo = "USD", Nombre = "Dólar", Activa = true });
            db.Monedas.Add(new Moneda { Id = 2, Codigo = "ARS", Nombre = "Peso", Activa = true });
            db.SaveChanges();
        }

        [Fact]
        public void ValidarVenta_SinHistorialDeCompras_SinAdvertencia()
        {
            var resultado = _pppService.ValidarVenta("XYZ", 1000m);
            Assert.True(string.IsNullOrEmpty(resultado.Mensaje) || !resultado.Mensaje.Contains("⚠️"));
        }

        [Fact]
        public void ObtenerPPP_MonedaSinHistorial_RetornaCero()
        {
            decimal ppp = _pppService.ObtenerPPP("MONEDA_QUE_NO_EXISTE");
            Assert.Equal(0m, ppp);
        }

        // ============================================
        // NUEVO: Tests de compra y venta (antes imposibles)
        // ============================================

        [Fact]
        public void RegistrarCompra_DeberiaCrearTenencia()
        {
            _pppService.RegistrarCompra("USD", 100m, 100000m);

            decimal ppp = _pppService.ObtenerPPP("USD");
            Assert.Equal(1000m, ppp);  // 100,000 / 100 = 1,000
        }

        [Fact]
        public void PPP_DespuesDeMultiplesCompras_DeberiaSerPromedioPonderado()
        {
            // Compra 1: 100 USD a $900 = $90,000
            _pppService.RegistrarCompra("USD", 100m, 90000m);
            
            // Compra 2: 200 USD a $1,100 = $220,000
            _pppService.RegistrarCompra("USD", 200m, 220000m);

            // PPP = (90,000 + 220,000) / (100 + 200) = 310,000 / 300 ≈ 1033.33
            decimal ppp = _pppService.ObtenerPPP("USD");
            Assert.True(Math.Abs(ppp - 1033.33m) < 0.01m);
        }

        [Fact]
        public void ValidarVenta_PorDebajoDelPPP_DeberiaAdvertir()
        {
            _pppService.RegistrarCompra("USD", 100m, 100000m);  // PPP = 1000

            var resultado = _pppService.ValidarVenta("USD", 900m);  // Venta por debajo

            Assert.Contains("⚠️", resultado.Mensaje);
            Assert.Equal(1000m, resultado.PPP);
        }

        [Fact]
        public void ValidarVenta_PorEncimaDelPPP_DeberiaSerRentable()
        {
            _pppService.RegistrarCompra("USD", 100m, 100000m);  // PPP = 1000

            var resultado = _pppService.ValidarVenta("USD", 1100m);  // Venta rentable

            Assert.Contains("Rentable", resultado.Mensaje);
        }
    }
}
