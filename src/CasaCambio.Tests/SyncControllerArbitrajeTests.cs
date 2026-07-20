using CasaCambio.Server.Controllers;
using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using CasaCambio.Server.Validators;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CasaCambio.Tests;

public class SyncControllerArbitrajeTests
{
    private const int IdCajaArs = 1;
    private const int IdCajaEur = 2;

    private readonly TestDbContextFactory _factory;
    private readonly SyncController _controller;

    public SyncControllerArbitrajeTests()
    {
        _factory = new TestDbContextFactory();
        var auditService = new AuditService(_factory);
        var cierreCajaService = new CierreCajaService(_factory, auditService);
        var validator = new OperacionValidator(_factory);
        var operacionService = new OperacionService(_factory, auditService, cierreCajaService, validator);
        var pppService = new PPPService(_factory);
        _controller = new SyncController(operacionService, pppService, _factory);

        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = IdCajaArs, Nombre = "EFECTIVO ARS", Tipo = "Efectivo" });
        db.Cuentas.Add(new Cuenta { Id = IdCajaEur, Nombre = "EFECTIVO EUR", Tipo = "Efectivo" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCajaArs, Moneda = "ARS", Saldo = 1000000m });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = IdCajaEur, Moneda = "EUR", Saldo = 20000m });
        db.SaveChanges();
    }

    [Fact]
    public void Push_ConOperacionArbitrajeOffline_CreaCompraYVentaVinculadasYActualizaSaldos()
    {
        decimal montoCompra = 10000m, cotCompra = 1800m;
        decimal pesos = montoCompra * cotCompra;
        decimal montoVenta = 10000m, cotVenta = 1800m;

        var request = new SyncPushRequest
        {
            Operaciones = new List<OperacionOfflineRequest>
            {
                new()
                {
                    LocalId = "local-arbitraje-1",
                    TipoOperacion = "Arbitraje",
                    // Pata Compra (forma existente)
                    CuentaOrigenId = IdCajaArs,       // CuentaPesosId
                    CuentaDestinoId = IdCajaEur,       // CuentaAcreditaCompraId
                    MonedaOrigen = "ARS",
                    MonedaDestino = "EUR",             // MonedaCompra
                    MontoOrigen = pesos,               // PesosCompra
                    MontoDestino = montoCompra,        // MontoExtranjeroCompra
                    CotizacionAplicada = cotCompra,    // CotizacionCompra
                    // Pata Venta (campos nuevos)
                    CuentaDebitaVentaId = IdCajaEur,
                    MonedaVenta = "EUR",
                    MontoExtranjeroVenta = montoVenta,
                    CotizacionVenta = cotVenta,
                    TipoOperacionArbitraje = "CLIENTE",
                    Observaciones = "Arbitraje offline",
                    FechaCreacionLocal = DateTime.UtcNow
                }
            }
        };

        var actionResult = _controller.Push(request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var response = Assert.IsType<SyncPushResponse>(objectResult.Value);
        var item = Assert.Single(response.Resultados);
        Assert.True(item.Exitoso, item.Mensaje);
        Assert.Equal("local-arbitraje-1", item.LocalId);
        Assert.NotNull(item.ServerOperacionId);

        using var db = _factory.CreateDbContext();
        Assert.Equal(2, db.Operaciones.Count());
        var opCompra = db.Operaciones.First(o => o.TipoOperacion == "Compra");
        var opVenta = db.Operaciones.First(o => o.TipoOperacion == "Venta");
        Assert.Equal(opVenta.Id, opCompra.OperacionParejaId);
        Assert.Equal(opCompra.Id, opVenta.OperacionParejaId);
        Assert.Equal(opCompra.Id, item.ServerOperacionId);

        // EFECTIVO EUR: +10000 (compra) -10000 (venta) = sin cambio neto
        var saldoEur = db.SaldosCuenta.First(s => s.CuentaId == IdCajaEur && s.Moneda == "EUR");
        Assert.Equal(20000m, saldoEur.Saldo);

        // EFECTIVO ARS: -pesos (compra) +pesos (venta) = sin cambio neto
        var saldoArs = db.SaldosCuenta.First(s => s.CuentaId == IdCajaArs && s.Moneda == "ARS");
        Assert.Equal(1000000m, saldoArs.Saldo);
    }
}
