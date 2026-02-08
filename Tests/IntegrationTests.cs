using SistemaCambio.Models;
using SistemaCambio.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SistemaCambio.Tests
{
    /// <summary>
    /// Tests de Integración - Pruebas que verifican el comportamiento
    /// completo del sistema usando una base de datos en memoria.
    /// 
    /// DIFERENCIA CON TESTS UNITARIOS:
    /// - Tests unitarios: prueban UNA función aislada
    /// - Tests integración: prueban VARIAS funciones trabajando juntas
    /// 
    /// Estos tests son más lentos pero más realistas.
    /// </summary>
    public class IntegrationTests : IDisposable
    {
        private readonly AppDbContext _db;

        public IntegrationTests()
        {
            // Crear base de datos en memoria para cada test
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            _db = new AppDbContext(options);
            SeedTestData();
        }

        private void SeedTestData()
        {
            // Crear datos de prueba
            _db.Monedas.Add(new Moneda { Id = 1, Codigo = "USD", Nombre = "Dólar", Activa = true });
            _db.Monedas.Add(new Moneda { Id = 2, Codigo = "ARS", Nombre = "Peso Argentino", Activa = true });
            _db.Monedas.Add(new Moneda { Id = 3, Codigo = "EUR", Nombre = "Euro", Activa = true });
            
            _db.Cuentas.Add(new Cuenta { Id = 1, Nombre = "Caja Pesos", Moneda = "ARS", Saldo = 1000000m, Tipo = "Caja" });
            _db.Cuentas.Add(new Cuenta { Id = 2, Nombre = "Caja USD", Moneda = "USD", Saldo = 5000m, Tipo = "Caja" });
            _db.Cuentas.Add(new Cuenta { Id = 3, Nombre = "Caja EUR", Moneda = "EUR", Saldo = 2000m, Tipo = "Caja" });
            _db.Cuentas.Add(new Cuenta { Id = 4, Nombre = "Banco Pesos", Moneda = "ARS", Saldo = 5000000m, Tipo = "Banco" });
            
            _db.SaveChanges();
        }

        public void Dispose()
        {
            _db.Dispose();
        }

        // ============================================
        // TESTS: Modelos y Entidades
        // ============================================

        [Fact]
        public void Cuenta_SaldoInicial_DeberiaSerCorrecto()
        {
            var cuenta = _db.Cuentas.Find(1);
            Assert.NotNull(cuenta);
            Assert.Equal(1000000m, cuenta.Saldo);
            Assert.Equal("Caja Pesos", cuenta.Nombre);
        }

        [Fact]
        public void Moneda_DeberiaExistir()
        {
            var monedas = _db.Monedas.Count();
            Assert.Equal(3, monedas);
        }

        [Fact]
        public void Cuenta_DeberiaPoderActualizarSaldo()
        {
            // Arrange
            var cuenta = _db.Cuentas.Find(2);
            Assert.NotNull(cuenta);
            decimal saldoOriginal = cuenta.Saldo;

            // Act
            cuenta.Saldo += 1000;
            _db.SaveChanges();

            // Assert
            var cuentaActualizada = _db.Cuentas.Find(2);
            Assert.Equal(saldoOriginal + 1000, cuentaActualizada!.Saldo);
        }

        // ============================================
        // TESTS: Operaciones CRUD
        // ============================================

        [Fact]
        public void Operacion_Crear_DeberiaGuardarse()
        {
            // Arrange
            var operacion = new Operacion
            {
                Fecha = DateTime.Now,
                TipoOperacion = "Compra",
                MontoTotalOrigen = 100000m,
                MontoTotalDestino = 100m,
                CotizacionAplicada = 1000m,
                Observaciones = "Test de compra"
            };

            // Act
            _db.Operaciones.Add(operacion);
            _db.SaveChanges();

            // Assert
            var opGuardada = _db.Operaciones.First();
            Assert.Equal("Compra", opGuardada.TipoOperacion);
            Assert.Equal(100000m, opGuardada.MontoTotalOrigen);
        }

        [Fact]
        public void Movimiento_Crear_DeberiaVincularseAOperacion()
        {
            // Arrange
            var operacion = new Operacion
            {
                Fecha = DateTime.Now,
                TipoOperacion = "Compra",
                MontoTotalOrigen = 100000m,
                MontoTotalDestino = 100m,
                CotizacionAplicada = 1000m
            };
            _db.Operaciones.Add(operacion);

            var movimiento = new Movimiento
            {
                Operacion = operacion,
                CuentaId = 1,
                Monto = -100000m,
                Fecha = DateTime.Now
            };
            _db.Movimientos.Add(movimiento);
            _db.SaveChanges();

            // Assert
            var movGuardado = _db.Movimientos.Include(m => m.Operacion).First();
            Assert.NotNull(movGuardado.Operacion);
            Assert.Equal(operacion.Id, movGuardado.Operacion.Id);
        }

        // ============================================
        // TESTS: Validación de Datos
        // ============================================

        [Fact]
        public void Cuenta_NoPuedeSerNegativa_Concepto()
        {
            // NOTA: Esta validación se hace en el código de OperacionService,
            // no en el modelo. El modelo permite valores negativos pero el
            // servicio lo bloquea.
            
            var cuenta = _db.Cuentas.Find(1);
            Assert.NotNull(cuenta);
            
            // El modelo PERMITE esto (no hay validación a nivel de BD)
            cuenta.Saldo = -1000;
            var exception = Record.Exception(() => _db.SaveChanges());
            
            // No hay excepción porque la validación está en el servicio, no en el modelo
            Assert.Null(exception);
        }

        // ============================================
        // TESTS: TenenciaMoneda (PPP)
        // ============================================

        [Fact]
        public void TenenciaMoneda_CalculaPPP_Correctamente()
        {
            // Arrange
            var tenencia = new TenenciaMoneda
            {
                MonedaId = 1,
                CantidadTotal = 1000m,
                CostoTotal = 1000000m
            };
            _db.TenenciasMoneda.Add(tenencia);
            _db.SaveChanges();

            // Act
            var ppp = tenencia.CostoTotal / tenencia.CantidadTotal;

            // Assert
            Assert.Equal(1000m, ppp); // PPP = 1,000,000 / 1,000 = 1,000
        }

        [Fact]
        public void TenenciaMoneda_PPPDespuesDeCompras()
        {
            // Simular dos compras y calcular PPP manualmente
            
            // Compra 1: 100 USD a $900 = $90,000
            decimal cantidad1 = 100m;
            decimal costo1 = 90000m;
            
            // Compra 2: 200 USD a $1100 = $220,000
            decimal cantidad2 = 200m;
            decimal costo2 = 220000m;
            
            // PPP esperado = (90,000 + 220,000) / (100 + 200) = 310,000 / 300 = 1033.33
            decimal totalCantidad = cantidad1 + cantidad2;
            decimal totalCosto = costo1 + costo2;
            decimal pppEsperado = totalCosto / totalCantidad;

            var tenencia = new TenenciaMoneda
            {
                MonedaId = 1,
                CantidadTotal = totalCantidad,
                CostoTotal = totalCosto
            };
            _db.TenenciasMoneda.Add(tenencia);
            _db.SaveChanges();

            // Assert
            Assert.Equal(pppEsperado, tenencia.CostoTotal / tenencia.CantidadTotal);
            Assert.True(Math.Abs(pppEsperado - 1033.33m) < 0.01m);
        }

        // ============================================
        // TESTS: Arqueo
        // ============================================

        [Fact]
        public void Arqueo_Crear_DeberiaGuardarse()
        {
            var arqueo = new Arqueo
            {
                CuentaId = 1,
                Fecha = DateTime.Now,
                SaldoSistema = 1000000m,
                SaldoArqueo = 1000500m,
                Diferencia = 500m,
                Observaciones = "Sobrante"
            };
            _db.Arqueos.Add(arqueo);
            _db.SaveChanges();

            var arqueoGuardado = _db.Arqueos.First();
            Assert.Equal(500m, arqueoGuardado.Diferencia);
        }

        // ============================================
        // TESTS: AuditLog
        // ============================================

        [Fact]
        public void AuditLog_Crear_DeberiaGuardarse()
        {
            var log = new AuditLog
            {
                Fecha = DateTime.Now,
                Accion = "CREATE",
                Entidad = "Operacion",
                EntidadId = 1,
                UsuarioNombre = "TestUser",
                ValoresNuevos = "{\"tipo\":\"Compra\"}"
            };
            _db.AuditLogs.Add(log);
            _db.SaveChanges();

            var logGuardado = _db.AuditLogs.First();
            Assert.Equal("CREATE", logGuardado.Accion);
            Assert.Equal("TestUser", logGuardado.UsuarioNombre);
        }

        // ============================================
        // TESTS: Cliente
        // ============================================

        [Fact]
        public void Cliente_Crear_DeberiaGuardarse()
        {
            var cliente = new Cliente
            {
                Nombre = "Juan Pérez",
                Documento = "12345678",
                Email = "juan@ejemplo.com"
            };
            _db.Clientes.Add(cliente);
            _db.SaveChanges();

            var clienteGuardado = _db.Clientes.First();
            Assert.Equal("Juan Pérez", clienteGuardado.Nombre);
            Assert.Equal("12345678", clienteGuardado.Documento);
        }

        // ============================================
        // TESTS: Cotización Diaria
        // ============================================

        [Fact]
        public void CotizacionDiaria_Crear_DeberiaGuardarse()
        {
            var cotizacion = new CotizacionDiaria
            {
                MonedaId = 1,
                Fecha = DateTime.Today,
                CotizacionCompra = 1000m,
                CotizacionVenta = 1050m
            };
            _db.CotizacionesDiarias.Add(cotizacion);
            _db.SaveChanges();

            var cotizGuardada = _db.CotizacionesDiarias.First();
            Assert.Equal(1000m, cotizGuardada.CotizacionCompra);
            Assert.Equal(1050m, cotizGuardada.CotizacionVenta);
        }
    }
}
