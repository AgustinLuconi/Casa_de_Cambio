using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using CasaCambio.Server.Validators;
using Xunit;

namespace CasaCambio.Tests;

public class ArbitrajeTests
{
    private const int IdCajaArs = 1;
    private const int IdCajaEur = 2;

    private readonly IOperacionService _operacionService;
    private readonly TestDbContextFactory _factory;

    public ArbitrajeTests()
    {
        _factory = new TestDbContextFactory();
        var auditService = new AuditService(_factory);
        var cierreCajaService = new CierreCajaService(_factory, auditService);
        var validator = new OperacionValidator(_factory);
        _operacionService = new OperacionService(_factory, auditService, cierreCajaService, validator);

        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = IdCajaArs, Nombre = "EFECTIVO ARS", Tipo = "Efectivo" });
        db.Cuentas.Add(new Cuenta { Id = IdCajaEur, Nombre = "EFECTIVO EUR", Tipo = "Efectivo" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCajaArs, Moneda = "ARS", Saldo = 1000000m });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCajaEur, Moneda = "EUR", Saldo = 20000m });
        db.SaveChanges();
    }

    private ArbitrajeResult Arbitrar(decimal montoCompra, decimal cotCompra, decimal montoVenta, decimal cotVenta) =>
        _operacionService.GuardarArbitraje(
            monedaCompra: "EUR", cuentaAcreditaCompraId: IdCajaEur, montoExtranjeroCompra: montoCompra, cotizacionCompra: cotCompra, pesosCompra: montoCompra * cotCompra,
            monedaVenta: "EUR", cuentaDebitaVentaId: IdCajaEur, montoExtranjeroVenta: montoVenta, cotizacionVenta: cotVenta, pesosVenta: montoVenta * cotVenta,
            cuentaPesosId: IdCajaArs, tipoOperacion: "CLIENTE", observaciones: "10K X 1.22");

    [Fact]
    public void GuardarArbitraje_PesosDistintos_RetornaError()
    {
        // Compra: 10000 EUR * 1800 = 18,000,000. Venta: 12000 EUR * 1475 = 17,700,000 (no coincide)
        var resultado = Arbitrar(montoCompra: 10000m, cotCompra: 1800m, montoVenta: 12000m, cotVenta: 1475m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("Pesos", resultado.Mensaje);
    }

    [Fact]
    public void GuardarArbitraje_PesosIguales_Exitoso_ActualizaSaldosYVinculaOperaciones()
    {
        // Compra: 10000 EUR * 1800 = 18,000,000. Venta: 12200 EUR * 1475.40984 ≈ 18,000,000
        decimal montoCompra = 10000m, cotCompra = 1800m;
        decimal pesos = montoCompra * cotCompra;
        decimal montoVenta = 12200m, cotVenta = Math.Round(pesos / montoVenta, 5, MidpointRounding.AwayFromZero);

        // Nota: pesosVenta se pasa como "pesos" (el monto que realmente debe coincidir), no como
        // Math.Round(montoVenta * cotVenta, 2) — recalcular vía la cotización redondeada a 5 decimales
        // y volver a multiplicar no siempre reproduce el monto original exacto (aquí da 18,000,000.05
        // en vez de 18,000,000.00), lo cual dispararía incorrectamente el chequeo de "Pesos distintos".
        // cotVenta se sigue calculando y enviando para que la operación de Venta quede con la cotización
        // real que hubiera correspondido, pero el monto en pesos que debe cuadrar es "pesos".
        var resultado = _operacionService.GuardarArbitraje(
            monedaCompra: "EUR", cuentaAcreditaCompraId: IdCajaEur, montoExtranjeroCompra: montoCompra, cotizacionCompra: cotCompra, pesosCompra: pesos,
            monedaVenta: "EUR", cuentaDebitaVentaId: IdCajaEur, montoExtranjeroVenta: montoVenta, cotizacionVenta: cotVenta, pesosVenta: pesos,
            cuentaPesosId: IdCajaArs, tipoOperacion: "CLIENTE", observaciones: "10K X 1.22");

        Assert.True(resultado.Exitoso);
        Assert.NotNull(resultado.OperacionIdCompra);
        Assert.NotNull(resultado.OperacionIdVenta);

        using var db = _factory.CreateDbContext();
        var opCompra = db.Operaciones.First(o => o.Id == resultado.OperacionIdCompra);
        var opVenta = db.Operaciones.First(o => o.Id == resultado.OperacionIdVenta);
        Assert.Equal("Compra", opCompra.TipoOperacion);
        Assert.Equal("Venta", opVenta.TipoOperacion);
        Assert.Equal(opVenta.Id, opCompra.OperacionParejaId);
        Assert.Equal(opCompra.Id, opVenta.OperacionParejaId);

        // EFECTIVO EUR: +10000 (compra) -12200 (venta) = 20000 - 2200 = 17800
        var saldoEur = db.SaldosCuenta.First(s => s.CuentaId == IdCajaEur && s.Moneda == "EUR");
        Assert.Equal(17800m, saldoEur.Saldo);

        // EFECTIVO ARS: -pesos (compra) +pesos (venta) = neto 0, saldo sin cambios
        var saldoArs = db.SaldosCuenta.First(s => s.CuentaId == IdCajaArs && s.Moneda == "ARS");
        Assert.Equal(1000000m, saldoArs.Saldo);
    }

    [Fact]
    public void GuardarArbitraje_VentaSinSaldoSuficiente_RetornaError_NoGuardaNada()
    {
        // La caja EUR tiene 20000, intenta vender 99999 (más de lo que tiene, sin límite de deuda por ser Efectivo)
        var resultado = Arbitrar(montoCompra: 10000m, cotCompra: 1800m, montoVenta: 99999m, cotVenta: 180.0018m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("Saldo insuficiente", resultado.Mensaje);

        using var db = _factory.CreateDbContext();
        Assert.Empty(db.Operaciones);
        var saldoEur = db.SaldosCuenta.First(s => s.CuentaId == IdCajaEur && s.Moneda == "EUR");
        Assert.Equal(20000m, saldoEur.Saldo);
    }

    [Fact]
    public void GuardarArbitraje_MismaCuentaYMoneda_CreditoCompraSeAplicaAntesDeValidarSaldoVenta()
    {
        // Reproduce el bug de aliasing de EF Core: cuentaAcreditaCompraId == cuentaDebitaVentaId y
        // monedaCompra == monedaVenta hacen que ObtenerOCrearSaldo devuelva el MISMO SaldoCuenta rastreado
        // para ambas patas. Si el chequeo de saldo de la Venta corre ANTES de aplicar el crédito de la
        // Compra, ve el saldo viejo (insuficiente) en vez del saldo real que la cuenta tendría después
        // de sumar la Compra.
        //
        // Saldo inicial bajo: 5000 EUR. Compra 10000 EUR, Venta 12000 EUR (a cotizaciones que igualan
        // PesosCompra == PesosVenta). Saldo final correcto: 5000 + 10000 - 12000 = 3000 (positivo, válido).
        // Con el bug, el chequeo evalúa 5000 < 12000 y rechaza con "Saldo insuficiente" aunque la
        // operación sea perfectamente válida.
        using (var db = _factory.CreateDbContext())
        {
            var saldoEur = db.SaldosCuenta.First(s => s.CuentaId == IdCajaEur && s.Moneda == "EUR");
            saldoEur.Saldo = 5000m;
            db.SaveChanges();
        }

        decimal montoCompra = 10000m, cotCompra = 1800m;
        decimal pesos = montoCompra * cotCompra;
        decimal montoVenta = 12000m, cotVenta = Math.Round(pesos / montoVenta, 5, MidpointRounding.AwayFromZero);

        var resultado = _operacionService.GuardarArbitraje(
            monedaCompra: "EUR", cuentaAcreditaCompraId: IdCajaEur, montoExtranjeroCompra: montoCompra, cotizacionCompra: cotCompra, pesosCompra: pesos,
            monedaVenta: "EUR", cuentaDebitaVentaId: IdCajaEur, montoExtranjeroVenta: montoVenta, cotizacionVenta: cotVenta, pesosVenta: pesos,
            cuentaPesosId: IdCajaArs, tipoOperacion: "CLIENTE", observaciones: "Aliasing compra/venta misma cuenta");

        Assert.True(resultado.Exitoso, resultado.Mensaje);

        using var dbVerif = _factory.CreateDbContext();
        var saldoFinal = dbVerif.SaldosCuenta.First(s => s.CuentaId == IdCajaEur && s.Moneda == "EUR");
        Assert.Equal(3000m, saldoFinal.Saldo);
    }

    [Fact]
    public void GuardarArbitraje_CuentaPesosInexistente_RetornaError()
    {
        var resultado = _operacionService.GuardarArbitraje(
            monedaCompra: "EUR", cuentaAcreditaCompraId: IdCajaEur, montoExtranjeroCompra: 100m, cotizacionCompra: 1800m, pesosCompra: 180000m,
            monedaVenta: "EUR", cuentaDebitaVentaId: IdCajaEur, montoExtranjeroVenta: 100m, cotizacionVenta: 1800m, pesosVenta: 180000m,
            cuentaPesosId: 999999, tipoOperacion: "CLIENTE");

        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public void AnularOperacion_ConPareja_AnulaAmbasEnCascada()
    {
        var creado = Arbitrar(montoCompra: 10000m, cotCompra: 1800m, montoVenta: 10000m, cotVenta: 1800m);
        Assert.True(creado.Exitoso);

        var resultadoAnular = _operacionService.AnularOperacion(creado.OperacionIdCompra!.Value);

        Assert.True(resultadoAnular.Exitoso);
        using var db = _factory.CreateDbContext();
        var opCompra = db.Operaciones.First(o => o.Id == creado.OperacionIdCompra);
        var opVenta = db.Operaciones.First(o => o.Id == creado.OperacionIdVenta);
        Assert.True(opCompra.Anulada);
        Assert.True(opVenta.Anulada, "La Venta debe anularse automáticamente al anular su pareja (la Compra).");

        // 2 anulaciones nuevas además de las 2 operaciones originales = 4 filas en total
        Assert.Equal(4, db.Operaciones.Count());

        // Saldos vuelven a su estado original: EFECTIVO EUR sin cambio neto, EFECTIVO ARS sin cambio neto
        var saldoEur = db.SaldosCuenta.First(s => s.CuentaId == IdCajaEur && s.Moneda == "EUR");
        Assert.Equal(20000m, saldoEur.Saldo);
        var saldoArs = db.SaldosCuenta.First(s => s.CuentaId == IdCajaArs && s.Moneda == "ARS");
        Assert.Equal(1000000m, saldoArs.Saldo);
    }

    [Fact]
    public void GuardarArbitraje_ConIdempotencyKeyRepetida_NoDuplicaOperaciones()
    {
        decimal montoCompra = 10000m, cotCompra = 1800m;
        decimal pesos = montoCompra * cotCompra;
        decimal montoVenta = 10000m, cotVenta = 1800m;

        var primero = _operacionService.GuardarArbitraje(
            monedaCompra: "EUR", cuentaAcreditaCompraId: IdCajaEur, montoExtranjeroCompra: montoCompra, cotizacionCompra: cotCompra, pesosCompra: pesos,
            monedaVenta: "EUR", cuentaDebitaVentaId: IdCajaEur, montoExtranjeroVenta: montoVenta, cotizacionVenta: cotVenta, pesosVenta: pesos,
            cuentaPesosId: IdCajaArs, tipoOperacion: "CLIENTE", observaciones: "idempotencia", idempotencyKey: "clave-fija");

        Assert.True(primero.Exitoso, primero.Mensaje);

        int countDespuesDelPrimero;
        using (var db = _factory.CreateDbContext())
        {
            countDespuesDelPrimero = db.Operaciones.Count();
        }

        var segundo = _operacionService.GuardarArbitraje(
            monedaCompra: "EUR", cuentaAcreditaCompraId: IdCajaEur, montoExtranjeroCompra: montoCompra, cotizacionCompra: cotCompra, pesosCompra: pesos,
            monedaVenta: "EUR", cuentaDebitaVentaId: IdCajaEur, montoExtranjeroVenta: montoVenta, cotizacionVenta: cotVenta, pesosVenta: pesos,
            cuentaPesosId: IdCajaArs, tipoOperacion: "CLIENTE", observaciones: "idempotencia", idempotencyKey: "clave-fija");

        Assert.True(segundo.Exitoso, segundo.Mensaje);
        Assert.Equal(primero.OperacionIdCompra, segundo.OperacionIdCompra);
        Assert.Equal(primero.OperacionIdVenta, segundo.OperacionIdVenta);

        using var dbFinal = _factory.CreateDbContext();
        Assert.Equal(countDespuesDelPrimero, dbFinal.Operaciones.Count());
    }

    [Fact]
    public void AnularOperacion_ParejaYaAnulada_NoIntentaAnularlaDeNuevo()
    {
        var creado = Arbitrar(montoCompra: 10000m, cotCompra: 1800m, montoVenta: 10000m, cotVenta: 1800m);
        Assert.True(creado.Exitoso);

        _operacionService.AnularOperacion(creado.OperacionIdCompra!.Value);
        // Intentar anular la Venta, que ya fue anulada en cascada — debe fallar con el mensaje existente, no duplicar la reversión.
        var segundoIntento = _operacionService.AnularOperacion(creado.OperacionIdVenta!.Value);

        Assert.False(segundoIntento.Exitoso);
        Assert.Contains("ya fue anulada", segundoIntento.Mensaje);
    }
}
