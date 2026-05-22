using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using Xunit;

namespace CasaCambio.Tests;

public class CierreCajaServiceTests
{
    private readonly ICierreCajaService _service;
    private readonly TestDbContextFactory _factory;

    public CierreCajaServiceTests()
    {
        _factory = new TestDbContextFactory();
        var auditService = new AuditService(_factory);
        _service = new CierreCajaService(_factory, auditService);

        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = 1, Nombre = "Caja ARS", Tipo = "Efectivo" });
        db.Cuentas.Add(new Cuenta { Id = 2, Nombre = "Caja USD", Tipo = "Efectivo" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 1, Moneda = "ARS", Saldo = 50000m });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 2, Moneda = "USD", Saldo = 200m });
        db.SaveChanges();
    }

    [Fact]
    public void GenerarCierre_SinOperaciones_CreaRegistroVacio()
    {
        var result = _service.GenerarCierre();

        Assert.True(result.Exitoso);
        Assert.NotNull(result.Cierre);
        Assert.Equal(0, result.Cierre!.CantidadCompras);
        Assert.Equal(0m, result.Cierre.TotalComprasARS);
        Assert.Equal(0m, result.Cierre.TotalComprasUSD);
        Assert.Equal(0, result.Cierre.CantidadVentas);
    }

    [Fact]
    public void GenerarCierre_ConCompras_TotalizaMontoOrigenYDestino()
    {
        using var db = _factory.CreateDbContext();
        db.Operaciones.Add(new Operacion
        {
            Fecha = DateTime.UtcNow,
            TipoOperacion = "Compra",
            MontoTotalOrigen = 10000m,   // ARS que sale
            MontoTotalDestino = 10m,     // USD que entra
            CotizacionAplicada = 1000m
        });
        db.SaveChanges();

        var result = _service.GenerarCierre();

        Assert.True(result.Exitoso);
        Assert.Equal(1, result.Cierre!.CantidadCompras);
        Assert.Equal(10000m, result.Cierre.TotalComprasARS);
        Assert.Equal(10m, result.Cierre.TotalComprasUSD);
    }

    [Fact]
    public void GenerarCierre_ConVentas_TotalizaOrigenComoUSDDestinoComoARS()
    {
        using var db = _factory.CreateDbContext();
        db.Operaciones.Add(new Operacion
        {
            Fecha = DateTime.UtcNow,
            TipoOperacion = "Venta",
            MontoTotalOrigen = 5m,       // USD que sale
            MontoTotalDestino = 5500m,   // ARS que entra
            CotizacionAplicada = 1100m
        });
        db.SaveChanges();

        var result = _service.GenerarCierre();

        Assert.True(result.Exitoso);
        Assert.Equal(1, result.Cierre!.CantidadVentas);
        Assert.Equal(5m, result.Cierre.TotalVentasUSD);
        Assert.Equal(5500m, result.Cierre.TotalVentasARS);
    }

    [Fact]
    public void GenerarCierre_SumaSaldosDeCajasEfectivo()
    {
        var result = _service.GenerarCierre();

        Assert.True(result.Exitoso);
        Assert.Equal(50000m, result.Cierre!.SaldoCajaARS);
        Assert.Equal(200m, result.Cierre.SaldoCajaUSD);
    }

    [Fact]
    public void GenerarCierre_SegundaLlamada_ActualizaEnVezDeCrear()
    {
        _service.GenerarCierre();
        _service.GenerarCierre();

        using var db = _factory.CreateDbContext();
        var cantidad = db.CierresCaja.Count();
        Assert.Equal(1, cantidad);
    }

    [Fact]
    public void CerrarDefinitivo_PoneFlag_Cerrado()
    {
        var genResult = _service.GenerarCierre();
        var cierreId = genResult.CierreId!.Value;

        var closeResult = _service.CerrarDefinitivo(cierreId);

        Assert.True(closeResult.Exitoso);
        using var db = _factory.CreateDbContext();
        var cierre = db.CierresCaja.Find(cierreId);
        Assert.True(cierre!.Cerrado);
    }

    [Fact]
    public void CerrarDefinitivo_CierreYaCerrado_RetornaError()
    {
        var genResult = _service.GenerarCierre();
        var cierreId = genResult.CierreId!.Value;
        _service.CerrarDefinitivo(cierreId);

        var result = _service.CerrarDefinitivo(cierreId);

        Assert.False(result.Exitoso);
        Assert.Contains("cerrado", result.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HayDiaCerrado_SinCierreDefinitivo_RetornaFalse()
    {
        _service.GenerarCierre();

        Assert.False(_service.HayDiaCerrado());
    }

    [Fact]
    public void HayDiaCerrado_ConCierreDefinitivo_RetornaTrue()
    {
        var genResult = _service.GenerarCierre();
        _service.CerrarDefinitivo(genResult.CierreId!.Value);

        Assert.True(_service.HayDiaCerrado());
    }
}
