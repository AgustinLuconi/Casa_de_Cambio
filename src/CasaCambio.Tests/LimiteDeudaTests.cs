using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using CasaCambio.Server.Validators;
using Xunit;

namespace CasaCambio.Tests;

/// <summary>
/// Cobertura de la cadena de herencia de límites de deuda por divisa:
///   1. límite específico cuenta+divisa (saldos_cuenta.limite_deuda)
///   2. límite global por divisa (configuracion: limite_deuda_general_{MONEDA})
///   3. legacy: escalar de la cuenta (cuentas.limite_deuda)
///   4. legacy: escalar global (configuracion: limite_deuda_general)
///   5. sin límite → operación a descubierto rechazada
/// </summary>
public class LimiteDeudaTests
{
    private const int IdCajaARS = 1;
    private const int IdCliente = 3;

    private readonly IOperacionService _operacionService;
    private readonly TestDbContextFactory _factory;

    public LimiteDeudaTests()
    {
        _factory = new TestDbContextFactory();
        var auditService = new AuditService(_factory);
        var cierreCajaService = new CierreCajaService(_factory, auditService);
        var validator = new OperacionValidator(_factory);
        _operacionService = new OperacionService(_factory, auditService, cierreCajaService, validator);

        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = IdCajaARS, Nombre = "Efectivo ARS", Tipo = "Efectivo" });
        db.Cuentas.Add(new Cuenta { Id = IdCliente, Nombre = "CLIENTE GOMEZ", Tipo = "Cliente" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCajaARS, Moneda = "ARS", Saldo = 10_000_000m });
        db.SaveChanges();
    }

    /// <summary>Venta de USD del cliente (sin saldo) contra la caja ARS — fuerza deuda.</summary>
    private OperacionResult VenderUsdDelCliente(decimal montoUsd) =>
        _operacionService.GuardarOperacion(
            tipo: "Venta", cuentaOrigenId: IdCliente, cuentaDestinoId: IdCajaARS,
            monedaOrigen: "USD", monedaDestino: "ARS",
            montoOrigen: montoUsd, montoDestino: montoUsd * 1000m, cotizacion: 1000m);

    private void DarLimiteEspecificoUsd(decimal limite)
    {
        using var db = _factory.CreateDbContext();
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCliente, Moneda = "USD", Saldo = 0, LimiteDeuda = limite });
        db.SaveChanges();
    }

    private void DarLimiteGlobal(string clave, decimal limite)
    {
        using var db = _factory.CreateDbContext();
        db.ConfiguracionSistema.Add(new ConfiguracionSistema
        {
            Clave = clave,
            Valor = limite.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        db.SaveChanges();
    }

    [Fact]
    public void ClienteSinLimite_NoPuedeOperarADescubierto()
    {
        var resultado = VenderUsdDelCliente(100m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("Saldo insuficiente", resultado.Mensaje);
    }

    [Fact]
    public void ClienteConLimiteEspecifico_PuedeEndeudarseDentroDelLimite()
    {
        DarLimiteEspecificoUsd(500m);

        var resultado = VenderUsdDelCliente(300m);

        Assert.True(resultado.Exitoso);
        using var db = _factory.CreateDbContext();
        var saldoUsd = db.SaldosCuenta.First(s => s.CuentaId == IdCliente && s.Moneda == "USD");
        Assert.Equal(-300m, saldoUsd.Saldo);
    }

    [Fact]
    public void ClienteConLimiteEspecifico_NoPuedeExcederlo()
    {
        DarLimiteEspecificoUsd(500m);

        var resultado = VenderUsdDelCliente(600m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("límite de deuda", resultado.Mensaje);
        Assert.Contains("USD", resultado.Mensaje);
    }

    [Fact]
    public void ClienteSinLimiteEspecifico_HeredaLimiteGlobalDeLaDivisa()
    {
        DarLimiteGlobal("limite_deuda_general_USD", 1000m);

        Assert.True(VenderUsdDelCliente(800m).Exitoso);
        Assert.False(VenderUsdDelCliente(900m).Exitoso); // deuda acumulada -1700 > 1000
    }

    [Fact]
    public void LimiteEspecifico_TienePrecedenciaSobreElGlobal()
    {
        DarLimiteEspecificoUsd(500m);
        DarLimiteGlobal("limite_deuda_general_USD", 5000m);

        // Si aplicara el global (5000) pasaría; el específico (500) debe bloquear
        var resultado = VenderUsdDelCliente(600m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("límite de deuda", resultado.Mensaje);
    }

    [Fact]
    public void LimiteGlobalDeOtraDivisa_NoAplica()
    {
        DarLimiteGlobal("limite_deuda_general_EUR", 5000m);

        var resultado = VenderUsdDelCliente(100m);

        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public void LimiteLegacyEscalarDeLaCuenta_SigueFuncionando()
    {
        using (var db = _factory.CreateDbContext())
        {
            var cliente = db.Cuentas.Find(IdCliente)!;
            cliente.LimiteDeuda = 400m;   // escalar pre-refactor
            db.SaveChanges();
        }

        Assert.True(VenderUsdDelCliente(300m).Exitoso);
        Assert.False(VenderUsdDelCliente(200m).Exitoso); // -500 excede 400
    }

    [Fact]
    public void CuentaNoCliente_NuncaOperaADescubierto_AunConLimiteGlobal()
    {
        DarLimiteGlobal("limite_deuda_general_ARS", 1_000_000_000m);

        // La caja Efectivo intenta comprar gastando más ARS de los que tiene
        var resultado = _operacionService.GuardarOperacion(
            tipo: "Compra", cuentaOrigenId: IdCajaARS, cuentaDestinoId: IdCliente,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 99_000_000m, montoDestino: 99_000m, cotizacion: 1000m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("Saldo insuficiente", resultado.Mensaje);
    }

    // ── Interbancaria: antes no validaba saldo/límite en absoluto ────────────

    [Fact]
    public void Interbancaria_CajaEfectivo_NuncaOperaADescubierto()
    {
        // La caja Efectivo intenta enviar más ARS de los que tiene vía Interbancaria
        var resultado = _operacionService.GuardarOperacionInterbancaria(
            tipo: "Interbancaria", cuentaOrigenId: IdCajaARS, cuentaDestinoId: IdCliente,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 99_000_000m, montoDestino: 99_000m, cotizacion: 1000m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("Saldo insuficiente", resultado.Mensaje);
    }

    [Fact]
    public void Interbancaria_ClienteConLimiteEspecifico_PuedeEndeudarseDentroDelLimite()
    {
        DarLimiteEspecificoUsd(500m);

        var resultado = _operacionService.GuardarOperacionInterbancaria(
            tipo: "Interbancaria", cuentaOrigenId: IdCajaARS, cuentaDestinoId: IdCliente,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 300_000m, montoDestino: 300m, cotizacion: 1000m);

        Assert.True(resultado.Exitoso);
        using var db = _factory.CreateDbContext();
        var saldoUsd = db.SaldosCuenta.First(s => s.CuentaId == IdCliente && s.Moneda == "USD");
        Assert.Equal(-300m, saldoUsd.Saldo);
    }

    [Fact]
    public void Interbancaria_ClienteConLimiteEspecifico_NoPuedeExcederlo()
    {
        DarLimiteEspecificoUsd(500m);

        var resultado = _operacionService.GuardarOperacionInterbancaria(
            tipo: "Interbancaria", cuentaOrigenId: IdCajaARS, cuentaDestinoId: IdCliente,
            monedaOrigen: "ARS", monedaDestino: "USD",
            montoOrigen: 600_000m, montoDestino: 600m, cotizacion: 1000m);

        Assert.False(resultado.Exitoso);
        Assert.Contains("límite de deuda", resultado.Mensaje);
    }
}
