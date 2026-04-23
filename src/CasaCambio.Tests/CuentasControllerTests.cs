using CasaCambio.Server.Controllers;
using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CasaCambio.Tests;

public class CuentasControllerTests
{
    private readonly TestDbContextFactory _factory;
    private readonly CuentasController _controller;

    public CuentasControllerTests()
    {
        _factory = new TestDbContextFactory();
        var audit = new AuditService(_factory);
        var arqueo = new ArqueoService(_factory, audit);
        var cierre = new CierreCajaService(_factory, audit);
        _controller = new CuentasController(_factory, arqueo, cierre);
    }

    [Fact]
    public void CrearCuenta_ConSaldosInicialesDistintosDeCero_GeneraMovimientosBalanceados()
    {
        var req = new CrearCuentaRequest
        {
            Nombre = "Banco XYZ", Tipo = "Banco",
            Saldos = new()
            {
                new SaldoCuentaDto { Moneda = "USD", Saldo = 1000m },
                new SaldoCuentaDto { Moneda = "ARS", Saldo = 0m }
            }
        };

        var result = _controller.CrearCuenta(req);

        Assert.IsType<CreatedAtActionResult>(result);
        using var db = _factory.CreateDbContext();
        var cuenta = db.Cuentas.First(c => c.Nombre == "BANCO XYZ");
        var saldoUSD = db.SaldosCuenta.First(s => s.CuentaId == cuenta.Id && s.Moneda == "USD");
        Assert.Equal(1000m, saldoUSD.Saldo);

        // Invariante: Σ movimientos(cuenta, moneda) == SaldoCuenta.Saldo
        var sumaMovs = db.Movimientos.Where(m => m.CuentaId == cuenta.Id && m.Moneda == "USD").Sum(m => m.Monto);
        Assert.Equal(saldoUSD.Saldo, sumaMovs);

        // Movimiento compensatorio en "Diferencias de Caja"
        var cuentaDif = db.Cuentas.First(c => c.Nombre == "Diferencias de Caja");
        var sumaDif = db.Movimientos.Where(m => m.CuentaId == cuentaDif.Id && m.Moneda == "USD").Sum(m => m.Monto);
        Assert.Equal(-1000m, sumaDif);

        // Saldo con Saldo == 0 no genera arqueo
        Assert.DoesNotContain(db.SaldosCuenta, s => s.CuentaId == cuenta.Id && s.Moneda == "ARS");
    }

    [Fact]
    public void ActualizarCuenta_ConDeltaSaldo_GeneraArqueo()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Cuentas.Add(new Cuenta { Id = 10, Nombre = "BANCO ABC", Tipo = "Banco" });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 10, Moneda = "USD", Saldo = 1000m });
            db.SaveChanges();
        }

        var req = new CrearCuentaRequest
        {
            Nombre = "Banco ABC", Tipo = "Banco",
            Saldos = new() { new SaldoCuentaDto { Moneda = "USD", Saldo = 1500m } }
        };

        var result = _controller.ActualizarCuenta(10, req);

        Assert.IsType<OkObjectResult>(result);
        using var db2 = _factory.CreateDbContext();
        var saldo = db2.SaldosCuenta.First(s => s.CuentaId == 10 && s.Moneda == "USD");
        Assert.Equal(1500m, saldo.Saldo);
        Assert.Single(db2.Arqueos.Where(a => a.CuentaId == 10));

        // Arqueo genera movimiento por el delta (500), no por el saldo total.
        var sumaMovs = db2.Movimientos.Where(m => m.CuentaId == 10 && m.Moneda == "USD").Sum(m => m.Monto);
        Assert.Equal(500m, sumaMovs);

        // Movimiento compensatorio en Diferencias de Caja
        var cuentaDif = db2.Cuentas.First(c => c.Nombre == "Diferencias de Caja");
        var sumaDif = db2.Movimientos.Where(m => m.CuentaId == cuentaDif.Id && m.Moneda == "USD").Sum(m => m.Monto);
        Assert.Equal(-500m, sumaDif);
    }

    [Fact]
    public void ActualizarCuenta_SaldoIgualAlActual_NoGeneraArqueo()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Cuentas.Add(new Cuenta { Id = 20, Nombre = "BANCO DEF", Tipo = "Banco" });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 20, Moneda = "USD", Saldo = 1000m });
            db.SaveChanges();
        }

        var req = new CrearCuentaRequest
        {
            Nombre = "Banco DEF", Tipo = "Banco",
            Saldos = new() { new SaldoCuentaDto { Moneda = "USD", Saldo = 1000m } }
        };

        var result = _controller.ActualizarCuenta(20, req);

        Assert.IsType<OkObjectResult>(result);
        using var db2 = _factory.CreateDbContext();
        Assert.Empty(db2.Arqueos.Where(a => a.CuentaId == 20));
    }

    [Fact]
    public void ActualizarCuenta_TipoEfectivoMultiMoneda_Retorna400()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Cuentas.Add(new Cuenta { Id = 30, Nombre = "EFECTIVO CAJA", Tipo = "Efectivo" });
            db.SaveChanges();
        }

        var req = new CrearCuentaRequest
        {
            Nombre = "Efectivo Caja", Tipo = "Efectivo",
            Saldos = new()
            {
                new SaldoCuentaDto { Moneda = "USD", Saldo = 100m },
                new SaldoCuentaDto { Moneda = "EUR", Saldo = 50m }
            }
        };

        var result = _controller.ActualizarCuenta(30, req);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
