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

    [Fact]
    public void EliminarCuenta_ConSaldoDistintoDeCero_Retorna400_AunSinMovimientos()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Cuentas.Add(new Cuenta { Id = 40, Nombre = "BANCO GHI", Tipo = "Banco" });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 40, Moneda = "USD", Saldo = 50m });
            db.SaveChanges();
        }

        var result = _controller.EliminarCuenta(40);

        Assert.IsType<BadRequestObjectResult>(result);
        using var db2 = _factory.CreateDbContext();
        Assert.NotNull(db2.Cuentas.Find(40));
    }

    [Fact]
    public void EliminarCuenta_SaldoCeroSinMovimientos_BorradoFisico()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Cuentas.Add(new Cuenta { Id = 41, Nombre = "BANCO JKL", Tipo = "Banco" });
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 41, Moneda = "USD", Saldo = 0m });
            db.SaveChanges();
        }

        var result = _controller.EliminarCuenta(41);

        Assert.IsType<NoContentResult>(result);
        using var db2 = _factory.CreateDbContext();
        Assert.Null(db2.Cuentas.Find(41));
    }

    [Fact]
    public void EliminarCuenta_SaldoCeroConMovimientos_BajaLogicaNoBorraFisicamente()
    {
        using (var db = _factory.CreateDbContext())
        {
            var cuenta = new Cuenta { Id = 42, Nombre = "BANCO MNO", Tipo = "Banco" };
            db.Cuentas.Add(cuenta);
            db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 42, Moneda = "USD", Saldo = 0m });
            var operacion = new Operacion { TipoOperacion = "Compra", MontoTotalOrigen = 100m, MontoTotalDestino = 100m, CotizacionAplicada = 1m };
            db.Operaciones.Add(operacion);
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = 42, Moneda = "USD", Monto = 100m });
            db.Movimientos.Add(new Movimiento { Operacion = operacion, CuentaId = 42, Moneda = "USD", Monto = -100m });
            db.SaveChanges();
        }

        var result = _controller.EliminarCuenta(42);

        Assert.IsType<NoContentResult>(result);
        using var db2 = _factory.CreateDbContext();
        var cuentaTrasBorrar = db2.Cuentas.Find(42);
        Assert.NotNull(cuentaTrasBorrar);
        Assert.False(cuentaTrasBorrar!.Activa);
        Assert.Equal(2, db2.Movimientos.Count(m => m.CuentaId == 42));
    }

    [Fact]
    public void GetCuentas_NoIncluyeCuentasDadasDeBaja()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Cuentas.Add(new Cuenta { Id = 43, Nombre = "BANCO PQR", Tipo = "Banco", Activa = false });
            db.SaveChanges();
        }

        var result = _controller.GetCuentas();

        var ok = Assert.IsType<OkObjectResult>(result);
        var cuentas = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<CuentaDto>>(ok.Value);
        Assert.DoesNotContain(cuentas, c => c.Id == 43);
    }
}
