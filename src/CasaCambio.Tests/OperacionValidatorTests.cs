using CasaCambio.Server.Models;
using CasaCambio.Server.Validators;
using Xunit;

namespace CasaCambio.Tests;

public class OperacionValidatorTests
{
    private readonly OperacionValidator _validator;
    private readonly TestDbContextFactory _factory;

    // IDs de cuentas creadas en seed
    private const int IdCuentaARS = 1;
    private const int IdCuentaUSD = 2;

    public OperacionValidatorTests()
    {
        _factory = new TestDbContextFactory();
        _validator = new OperacionValidator(_factory);

        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = IdCuentaARS, Nombre = "Caja ARS", Tipo = "Efectivo" });
        db.Cuentas.Add(new Cuenta { Id = IdCuentaUSD, Nombre = "Caja USD", Tipo = "Efectivo" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCuentaARS, Moneda = "ARS", Saldo = 100m });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCuentaUSD, Moneda = "USD", Saldo = 50m });
        db.SaveChanges();
    }

    // ── ValidarOperacion ────────────────────────────────────────────

    [Fact]
    public void ValidarOperacion_CuentaOrigenInexistente_RetornaError()
    {
        var result = _validator.ValidarOperacion("Compra",
            cuentaOrigenId: 9999, cuentaDestinoId: IdCuentaUSD,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 1m, montoDestino: 1m, cotizacion: 1m);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidarOperacion_MontoOrigenNegativo_RetornaError()
    {
        var result = _validator.ValidarOperacion("Compra",
            cuentaOrigenId: IdCuentaARS, cuentaDestinoId: IdCuentaUSD,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: -1m, montoDestino: 1m, cotizacion: 1m);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidarOperacion_CotizacionNegativa_RetornaError()
    {
        var result = _validator.ValidarOperacion("Compra",
            cuentaOrigenId: IdCuentaARS, cuentaDestinoId: IdCuentaUSD,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 10m, montoDestino: 1m, cotizacion: -5m);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidarOperacion_SaldoInsuficiente_RetornaError()
    {
        // La caja ARS tiene 100 ARS; pedimos 200
        var result = _validator.ValidarOperacion("Compra",
            cuentaOrigenId: IdCuentaARS, cuentaDestinoId: IdCuentaUSD,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 200m, montoDestino: 1m, cotizacion: 200m);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidarOperacion_Compra_MonedaOrigenNoARS_RetornaError()
    {
        var result = _validator.ValidarOperacion("Compra",
            cuentaOrigenId: IdCuentaUSD, cuentaDestinoId: IdCuentaARS,
            monedaOrigen: "USD", monedaDestino: "ARS",
            montoOrigen: 1m, montoDestino: 1000m, cotizacion: 1000m);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidarOperacion_Venta_MonedaDestinoNoARS_RetornaError()
    {
        // Venta donde el destino no es ARS (inválido)
        var result = _validator.ValidarOperacion("Venta",
            cuentaOrigenId: IdCuentaUSD, cuentaDestinoId: IdCuentaARS,
            monedaOrigen: "USD", monedaDestino: "USD",
            montoOrigen: 1m, montoDestino: 1m, cotizacion: 1m);

        Assert.True(result.HasErrors);
    }

    // ── ValidarCreditoDebito ────────────────────────────────────────

    [Fact]
    public void ValidarCreditoDebito_CuentaInexistente_RetornaError()
    {
        var result = _validator.ValidarCreditoDebito(
            cuentaCreditoId: 9999, cuentaDebitoId: IdCuentaARS,
            monedaCredito: "ARS", monedaDebito: "ARS",
            montoCredito: 10m, montoDebito: 10m);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidarCreditoDebito_MonedasDistintasSinARS_RetornaError()
    {
        // USD → EUR sin ARS: debe bloquearse
        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = 3, Nombre = "Caja EUR", Tipo = "Efectivo" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 3, Moneda = "EUR", Saldo = 20m });
        db.SaveChanges();

        var result = _validator.ValidarCreditoDebito(
            cuentaCreditoId: IdCuentaUSD, cuentaDebitoId: 3,
            monedaCredito: "USD", monedaDebito: "EUR",
            montoCredito: 10m, montoDebito: 10m);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidarCreditoDebito_MismaCuentaMismaMoneda_RetornaError()
    {
        var result = _validator.ValidarCreditoDebito(
            cuentaCreditoId: IdCuentaARS, cuentaDebitoId: IdCuentaARS,
            monedaCredito: "ARS", monedaDebito: "ARS",
            montoCredito: 10m, montoDebito: 10m);

        Assert.True(result.HasErrors);
    }

    // ── ValidarOperacionInterbancaria ───────────────────────────────

    [Fact]
    public void ValidarInterbancaria_MismaMoneda_RetornaError()
    {
        var result = _validator.ValidarOperacionInterbancaria(
            cuentaOrigenId: IdCuentaARS, cuentaDestinoId: IdCuentaUSD,
            monedaOrigen: "ARS", monedaDestino: "ARS",
            montoOrigen: 10m, montoDestino: 10m);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidarInterbancaria_SinARS_RetornaError()
    {
        // USD ↔ EUR sin ARS: debe bloquearse
        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = 4, Nombre = "Caja EUR 2", Tipo = "Efectivo" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 4, Moneda = "EUR", Saldo = 30m });
        db.SaveChanges();

        var result = _validator.ValidarOperacionInterbancaria(
            cuentaOrigenId: IdCuentaUSD, cuentaDestinoId: 4,
            monedaOrigen: "USD", monedaDestino: "EUR",
            montoOrigen: 5m, montoDestino: 5m);

        Assert.True(result.HasErrors);
    }
}
