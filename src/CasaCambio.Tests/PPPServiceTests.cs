using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using Xunit;

namespace CasaCambio.Tests;

public class PPPServiceTests
{
    private readonly IPPPService _pppService;
    private readonly TestDbContextFactory _factory;

    public PPPServiceTests()
    {
        _factory = new TestDbContextFactory();
        _pppService = new PPPService(_factory);

        using var db = _factory.CreateDbContext();
        db.Monedas.Add(new Moneda { Id = 1, Codigo = "USD", Nombre = "Dolar", Activa = true });
        db.Monedas.Add(new Moneda { Id = 2, Codigo = "ARS", Nombre = "Peso", Activa = true });
        db.SaveChanges();
    }

    [Fact]
    public void ObtenerPPP_MonedaSinHistorial_RetornaCero()
    {
        decimal ppp = _pppService.ObtenerPPP("MONEDA_QUE_NO_EXISTE");
        Assert.Equal(0m, ppp);
    }

    [Fact]
    public void RegistrarCompra_DeberiaCrearTenencia()
    {
        _pppService.RegistrarCompra("USD", 100m, 100000m);

        decimal ppp = _pppService.ObtenerPPP("USD");
        Assert.Equal(1000m, ppp);
    }

    [Fact]
    public void PPP_DespuesDeMultiplesCompras_DeberiaSerPromedioPonderado()
    {
        _pppService.RegistrarCompra("USD", 100m, 90000m);
        _pppService.RegistrarCompra("USD", 200m, 220000m);

        decimal ppp = _pppService.ObtenerPPP("USD");
        Assert.True(Math.Abs(ppp - 1033.33m) < 0.01m);
    }

    [Fact]
    public void ValidarVenta_PorDebajoDelPPP_DeberiaAdvertir()
    {
        _pppService.RegistrarCompra("USD", 100m, 100000m);

        var resultado = _pppService.ValidarVenta("USD", 900m);

        Assert.Contains("ALERTA", resultado.Mensaje);
        Assert.Equal(1000m, resultado.PPP);
    }

    [Fact]
    public void ValidarVenta_PorEncimaDelPPP_DeberiaSerRentable()
    {
        _pppService.RegistrarCompra("USD", 100m, 100000m);

        var resultado = _pppService.ValidarVenta("USD", 1100m);

        Assert.Contains("Rentable", resultado.Mensaje);
    }

    [Fact]
    public void ValidarVenta_SinHistorial_Valido()
    {
        var resultado = _pppService.ValidarVenta("XYZ", 1000m);
        Assert.True(resultado.Valido);
    }

    [Fact]
    public void RegistrarVenta_DeberiaReducirTenencia()
    {
        _pppService.RegistrarCompra("USD", 100m, 100000m);
        _pppService.RegistrarVenta("USD", 30m);

        using var db = _factory.CreateDbContext();
        var tenencia = db.TenenciasMoneda.First(t => t.MonedaId == 1);
        Assert.Equal(70m, tenencia.CantidadTotal);
        Assert.Equal(70000m, tenencia.CostoTotal);
    }
}
