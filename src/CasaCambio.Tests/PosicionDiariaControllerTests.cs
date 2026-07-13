using CasaCambio.Server.Controllers;
using CasaCambio.Server.Models;
using CasaCambio.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CasaCambio.Tests;

public class PosicionDiariaControllerTests
{
    private readonly TestDbContextFactory _factory;
    private readonly PosicionDiariaController _controller;

    public PosicionDiariaControllerTests()
    {
        _factory = new TestDbContextFactory();
        _controller = new PosicionDiariaController(_factory);

        using var db = _factory.CreateDbContext();
        db.Monedas.Add(new Moneda { Id = 1, Codigo = "USD", Nombre = "Dólar", Activa = true, TipoPase = "D" });
        db.Monedas.Add(new Moneda { Id = 2, Codigo = "EUR", Nombre = "Euro", Activa = true, TipoPase = "M" });

        var cajaEfectivo = new Cuenta { Id = 1, Nombre = "EFECTIVO USD", Tipo = "Efectivo" };
        var cuentaCliente = new Cuenta { Id = 2, Nombre = "CLIENTE X", Tipo = "Cliente" };
        db.Cuentas.Add(cajaEfectivo);
        db.Cuentas.Add(cuentaCliente);

        var op = new Operacion { TipoOperacion = "Compra", MontoTotalOrigen = 1m, MontoTotalDestino = 1m, CotizacionAplicada = 1m };
        db.Operaciones.Add(op);

        // Antes del rango (desde=2026-06-10): debe entrar en Cap Inicial y Cap Final
        db.Movimientos.Add(new Movimiento { Operacion = op, CuentaId = 1, Moneda = "USD", Monto = 1000m, Fecha = new DateTime(2026, 6, 1) });
        // Dentro del rango (entre desde y hasta): entra solo en Cap Final
        db.Movimientos.Add(new Movimiento { Operacion = op, CuentaId = 1, Moneda = "USD", Monto = 500m, Fecha = new DateTime(2026, 6, 15) });
        // Cuenta Cliente (no Efectivo): NO debe contarse en ningún lado
        db.Movimientos.Add(new Movimiento { Operacion = op, CuentaId = 2, Moneda = "USD", Monto = 9999m, Fecha = new DateTime(2026, 6, 10) });

        db.SaveChanges();
    }

    [Fact]
    public void GetPosicionDiaria_CalculaCapInicialYFinal_SoloConCuentasEfectivo()
    {
        var result = _controller.GetPosicionDiaria(desde: new DateTime(2026, 6, 10), hasta: new DateTime(2026, 6, 20));

        var ok = Assert.IsType<OkObjectResult>(result);
        var posiciones = Assert.IsAssignableFrom<System.Collections.Generic.List<PosicionDiariaDto>>(ok.Value);

        var usd = Assert.Single(posiciones, p => p.Codigo == "USD");
        Assert.Equal(1000m, usd.CapInicial);
        Assert.Equal(1500m, usd.CapFinal);
        Assert.Equal("D", usd.TipoPase);
    }

    [Fact]
    public void GetPosicionDiaria_MonedaSinMovimientos_DevuelveCeros()
    {
        var result = _controller.GetPosicionDiaria(desde: new DateTime(2026, 6, 10), hasta: new DateTime(2026, 6, 20));

        var ok = Assert.IsType<OkObjectResult>(result);
        var posiciones = Assert.IsAssignableFrom<System.Collections.Generic.List<PosicionDiariaDto>>(ok.Value);

        var eur = Assert.Single(posiciones, p => p.Codigo == "EUR");
        Assert.Equal(0m, eur.CapInicial);
        Assert.Equal(0m, eur.CapFinal);
        Assert.Equal("M", eur.TipoPase);
    }
}
