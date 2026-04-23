using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using CasaCambio.Server.Validators;
using Xunit;

namespace CasaCambio.Tests;

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

        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = 1, Nombre = "Efectivo ARS", Tipo = "Efectivo" });
        db.Cuentas.Add(new Cuenta { Id = 2, Nombre = "Efectivo USD", Tipo = "Efectivo" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 1, Moneda = "ARS", Saldo = 1000000m });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 2, Moneda = "USD", Saldo = 5000m });
        db.SaveChanges();
    }

    [Fact]
    public void GuardarOperacion_CuentaOrigenNoExiste_DeberiaRetornarError()
    {
        var resultado = _operacionService.GuardarOperacion(
            tipo: "Compra", cuentaOrigenId: 999999, cuentaDestinoId: 1,
            monedaOrigen: "USD", monedaDestino: "ARS",
            montoOrigen: 100m, montoDestino: 0.1m, cotizacion: 1000m);

        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public void GuardarOperacion_CuentaDestinoNoExiste_DeberiaRetornarError()
    {
        var resultado = _operacionService.GuardarOperacion(
            tipo: "Compra", cuentaOrigenId: 1, cuentaDestinoId: 999999,
            monedaOrigen: "USD", monedaDestino: "ARS",
            montoOrigen: 100m, montoDestino: 0.1m, cotizacion: 1000m);

        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public void GuardarOperacion_Exitosa_DeberiaActualizarSaldos()
    {
        var resultado = _operacionService.GuardarOperacion(
            tipo: "Compra", cuentaOrigenId: 1, cuentaDestinoId: 2,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 1000m, montoDestino: 1m, cotizacion: 1000m);

        Assert.True(resultado.Exitoso);
        Assert.NotNull(resultado.OperacionId);

        using var db = _factory.CreateDbContext();
        var saldoPesos = db.SaldosCuenta.First(s => s.CuentaId == 1 && s.Moneda == "ARS");
        var saldoUSD = db.SaldosCuenta.First(s => s.CuentaId == 2 && s.Moneda == "USD");
        Assert.Equal(999000m, saldoPesos.Saldo);
        Assert.Equal(5001m, saldoUSD.Saldo);
    }

    [Fact]
    public void GuardarOperacion_SaldoInsuficiente_DeberiaRetornarError()
    {
        var resultado = _operacionService.GuardarOperacion(
            tipo: "Compra", cuentaOrigenId: 1, cuentaDestinoId: 2,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 9999999m, montoDestino: 1m, cotizacion: 1m);

        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public void GuardarOperacion_Exitosa_DeberiaCear2Movimientos()
    {
        var resultado = _operacionService.GuardarOperacion(
            tipo: "Compra", cuentaOrigenId: 1, cuentaDestinoId: 2,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 1000m, montoDestino: 1m, cotizacion: 1000m);

        using var db = _factory.CreateDbContext();
        var movimientos = db.Movimientos.Where(m => m.OperacionId == resultado.OperacionId).ToList();
        Assert.Equal(2, movimientos.Count);
    }

    [Fact]
    public void GuardarCreditoDebito_CuentaNoExiste_DeberiaRetornarError()
    {
        var resultado = _operacionService.GuardarCreditoDebito(
            cuentaCreditoId: 999999, cuentaDebitoId: 1,
            monedaCredito: "ARS", monedaDebito: "ARS",
            montoCredito: 100m, montoDebito: 100m, cotizacion: 1m);

        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public void GuardarCreditoDebito_MismaMoneda_Exitoso()
    {
        var resultado = _operacionService.GuardarCreditoDebito(
            cuentaCreditoId: 2, cuentaDebitoId: 1,
            monedaCredito: "ARS", monedaDebito: "ARS",
            montoCredito: 500m, montoDebito: 500m, cotizacion: 1m);

        Assert.True(resultado.Exitoso);

        using var db = _factory.CreateDbContext();
        var saldoOrigen = db.SaldosCuenta.First(s => s.CuentaId == 1 && s.Moneda == "ARS");
        Assert.Equal(999500m, saldoOrigen.Saldo);
    }

    [Fact]
    public void GuardarOperacion_DeberiaRedondearMontos()
    {
        var resultado = _operacionService.GuardarOperacion(
            tipo: "Compra", cuentaOrigenId: 1, cuentaDestinoId: 2,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 1000.555m, montoDestino: 1.005m, cotizacion: 995.57777m);

        Assert.True(resultado.Exitoso);

        using var db = _factory.CreateDbContext();
        var op = db.Operaciones.First(o => o.Id == resultado.OperacionId);
        Assert.Equal(1000.56m, op.MontoTotalOrigen);
        Assert.Equal(1.01m, op.MontoTotalDestino);
        Assert.Equal(995.57777m, op.CotizacionAplicada);
    }

    [Fact]
    public void GuardarCreditoDebito_MonedasDistintasSinARS_DeberiaRetornarError()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Cuentas.Add(new Cuenta { Id = 3, Nombre = "Efectivo EUR", Tipo = "Banco" });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 3, Moneda = "EUR", Saldo = 1000m });
            db.SaveChanges();
        }

        var resultado = _operacionService.GuardarCreditoDebito(
            cuentaCreditoId: 3, cuentaDebitoId: 2,
            monedaCredito: "EUR", monedaDebito: "USD",
            montoCredito: 100m, montoDebito: 110m, cotizacion: 1.1m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("ARS", resultado.Mensaje);
    }

    [Fact]
    public void GuardarCreditoDebito_UnaMonedaEsARS_Exitoso()
    {
        var resultado = _operacionService.GuardarCreditoDebito(
            cuentaCreditoId: 1, cuentaDebitoId: 2,
            monedaCredito: "ARS", monedaDebito: "USD",
            montoCredito: 100000m, montoDebito: 100m, cotizacion: 1000m);

        Assert.True(resultado.Exitoso);
    }

    [Fact]
    public void GuardarOperacionInterbancaria_SinARS_DeberiaRetornarError()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Cuentas.Add(new Cuenta { Id = 4, Nombre = "Banco EUR", Tipo = "Banco" });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 4, Moneda = "EUR", Saldo = 1000m });
            db.SaveChanges();
        }

        var resultado = _operacionService.GuardarOperacionInterbancaria(
            tipo: "Interbancaria", cuentaOrigenId: 2, cuentaDestinoId: 4,
            monedaOrigen: "USD", monedaDestino: "EUR",
            montoOrigen: 100m, montoDestino: 90m, cotizacion: 0.9m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("ARS", resultado.Mensaje);
    }

    [Fact]
    public void GuardarOperacionInterbancaria_ConARS_Exitoso()
    {
        var resultado = _operacionService.GuardarOperacionInterbancaria(
            tipo: "Interbancaria", cuentaOrigenId: 2, cuentaDestinoId: 1,
            monedaOrigen: "USD", monedaDestino: "ARS",
            montoOrigen: 100m, montoDestino: 100000m, cotizacion: 1000m);

        Assert.True(resultado.Exitoso);
    }
}
