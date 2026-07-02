using CasaCambio.Server.Data;
using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using CasaCambio.Server.Validators;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaCambio.Tests;

public class IntegrationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TestDbContextFactory _factory;

    public IntegrationTests()
    {
        _factory = new TestDbContextFactory();
        _db = _factory.CreateDbContext();
        SeedTestData();
    }

    private void SeedTestData()
    {
        _db.Monedas.Add(new Moneda { Id = 1, Codigo = "USD", Nombre = "Dolar", Activa = true });
        _db.Monedas.Add(new Moneda { Id = 2, Codigo = "ARS", Nombre = "Peso Argentino", Activa = true });
        _db.Monedas.Add(new Moneda { Id = 3, Codigo = "EUR", Nombre = "Euro", Activa = true });

        _db.Cuentas.Add(new Cuenta { Id = 1, Nombre = "Efectivo ARS", Tipo = "Efectivo" });
        _db.Cuentas.Add(new Cuenta { Id = 2, Nombre = "Efectivo USD", Tipo = "Efectivo" });
        _db.Cuentas.Add(new Cuenta { Id = 3, Nombre = "Efectivo EUR", Tipo = "Efectivo" });
        _db.Cuentas.Add(new Cuenta { Id = 4, Nombre = "Banco Pesos", Tipo = "Banco" });

        _db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 1, Moneda = "ARS", Saldo = 1000000m });
        _db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 2, Moneda = "USD", Saldo = 5000m });
        _db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 3, Moneda = "EUR", Saldo = 2000m });
        _db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 4, Moneda = "ARS", Saldo = 5000000m });

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Cuenta_SaldoInicial_DeberiaSerCorrecto()
    {
        var cuenta = _db.Cuentas.Find(1);
        Assert.NotNull(cuenta);
        var saldo = _db.SaldosCuenta.First(s => s.CuentaId == 1 && s.Moneda == "ARS");
        Assert.Equal(1000000m, saldo.Saldo);
        Assert.Equal("Efectivo ARS", cuenta.Nombre);
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
        var saldoCuenta = _db.SaldosCuenta.First(s => s.CuentaId == 2 && s.Moneda == "USD");
        decimal saldoOriginal = saldoCuenta.Saldo;

        saldoCuenta.Saldo += 1000;
        _db.SaveChanges();

        var actualizado = _db.SaldosCuenta.First(s => s.CuentaId == 2 && s.Moneda == "USD");
        Assert.Equal(saldoOriginal + 1000, actualizado.Saldo);
    }

    [Fact]
    public void Operacion_Crear_DeberiaGuardarse()
    {
        var operacion = new Operacion
        {
            Fecha = DateTime.UtcNow, TipoOperacion = "Compra",
            MontoTotalOrigen = 100000m, MontoTotalDestino = 100m,
            CotizacionAplicada = 1000m, Observaciones = "Test"
        };
        _db.Operaciones.Add(operacion);
        _db.SaveChanges();

        var guardada = _db.Operaciones.First();
        Assert.Equal("Compra", guardada.TipoOperacion);
        Assert.Equal(100000m, guardada.MontoTotalOrigen);
    }

    [Fact]
    public void Movimiento_Crear_DeberiaVincularseAOperacion()
    {
        var operacion = new Operacion
        {
            Fecha = DateTime.UtcNow, TipoOperacion = "Compra",
            MontoTotalOrigen = 100000m, MontoTotalDestino = 100m,
            CotizacionAplicada = 1000m
        };
        _db.Operaciones.Add(operacion);
        _db.Movimientos.Add(new Movimiento
        {
            Operacion = operacion, CuentaId = 1,
            Monto = -100000m, Fecha = DateTime.UtcNow
        });
        _db.SaveChanges();

        var mov = _db.Movimientos.Include(m => m.Operacion).First();
        Assert.NotNull(mov.Operacion);
        Assert.Equal(operacion.Id, mov.Operacion.Id);
    }

    [Fact]
    public void TenenciaMoneda_CalculaPPP_Correctamente()
    {
        _db.TenenciasMoneda.Add(new TenenciaMoneda
        {
            MonedaId = 1, CantidadTotal = 1000m, CostoTotal = 1000000m
        });
        _db.SaveChanges();

        var tenencia = _db.TenenciasMoneda.First();
        Assert.Equal(1000m, tenencia.CostoTotal / tenencia.CantidadTotal);
    }

    [Fact]
    public void CotizacionDiaria_Crear_DeberiaGuardarse()
    {
        _db.CotizacionesDiarias.Add(new CotizacionDiaria
        {
            MonedaId = 1, Fecha = DateTime.Today,
            CotizacionCompra = 1000m, CotizacionVenta = 1050m
        });
        _db.SaveChanges();

        var cotiz = _db.CotizacionesDiarias.First();
        Assert.Equal(1000m, cotiz.CotizacionCompra);
        Assert.Equal(1050m, cotiz.CotizacionVenta);
    }

    [Fact]
    public void Arqueo_Crear_DeberiaGuardarse()
    {
        _db.Arqueos.Add(new Arqueo
        {
            CuentaId = 1, Fecha = DateTime.UtcNow,
            SaldoSistema = 1000000m, SaldoArqueo = 1000500m,
            Diferencia = 500m, Observaciones = "Sobrante"
        });
        _db.SaveChanges();

        var arqueo = _db.Arqueos.First();
        Assert.Equal(500m, arqueo.Diferencia);
    }

    [Fact]
    public void FlujoCompleto_CompraVenta_DeberiaActualizarSaldosYPPP()
    {
        var auditService = new AuditService(_factory);
        var cierreCajaService = new CierreCajaService(_factory, auditService);
        var validator = new OperacionValidator(_factory);
        var operacionService = new OperacionService(_factory, auditService, cierreCajaService, validator);
        var pppService = new PPPService(_factory);

        // Compra: 1000 ARS -> 1 USD (cotizacion 1000)
        var resultCompra = operacionService.GuardarOperacion(
            "Compra", cuentaOrigenId: 1, cuentaDestinoId: 2,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 10000m, montoDestino: 10m, cotizacion: 1000m);
        Assert.True(resultCompra.Exitoso);

        pppService.RegistrarCompra("USD", 10m, 10000m);

        // Verificar saldos
        using var db = _factory.CreateDbContext();
        var saldoPesos = db.SaldosCuenta.First(s => s.CuentaId == 1 && s.Moneda == "ARS");
        var saldoUSD = db.SaldosCuenta.First(s => s.CuentaId == 2 && s.Moneda == "USD");
        Assert.Equal(990000m, saldoPesos.Saldo);
        Assert.Equal(5010m, saldoUSD.Saldo);

        // Verificar PPP
        decimal ppp = pppService.ObtenerPPP("USD");
        Assert.Equal(1000m, ppp);

        // Validar que la venta a 1100 es rentable
        var validacion = pppService.ValidarVenta("USD", 1100m);
        Assert.Contains("Rentable", validacion.Mensaje);
    }
}
