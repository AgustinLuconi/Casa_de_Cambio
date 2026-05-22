using CasaCambio.Server.Models;
using CasaCambio.Server.Services;
using Xunit;

namespace CasaCambio.Tests;

public class ArqueoServiceTests
{
    private readonly IArqueoService _service;
    private readonly TestDbContextFactory _factory;

    public ArqueoServiceTests()
    {
        _factory = new TestDbContextFactory();
        var auditService = new AuditService(_factory);
        _service = new ArqueoService(_factory, auditService);

        using var db = _factory.CreateDbContext();
        db.Cuentas.Add(new Cuenta { Id = 1, Nombre = "Caja USD", Tipo = "Efectivo" });
        db.SaldosCuenta.Add(new SaldoCuenta { CuentaId = 1, Moneda = "USD", Saldo = 100m });
        db.SaveChanges();
    }

    [Fact]
    public void RealizarArqueo_CuentaNoExiste_RetornaError()
    {
        var result = _service.RealizarArqueoCiego(cuentaId: 9999, moneda: "USD", montoContado: 100m);

        Assert.False(result.Exitoso);
    }

    [Fact]
    public void RealizarArqueo_SaldoCuadra_NoCreaMovimientos()
    {
        var result = _service.RealizarArqueoCiego(cuentaId: 1, moneda: "USD", montoContado: 100m);

        Assert.True(result.Exitoso);
        Assert.Equal(0m, result.Diferencia);
        using var db = _factory.CreateDbContext();
        Assert.Empty(db.Movimientos.ToList());
    }

    [Fact]
    public void RealizarArqueo_Sobrante_CreaMovimientosYAjustaSaldo()
    {
        // Contamos 110 USD pero el sistema dice 100 → sobrante de 10
        var result = _service.RealizarArqueoCiego(cuentaId: 1, moneda: "USD", montoContado: 110m);

        Assert.True(result.Exitoso);
        Assert.Equal(10m, result.Diferencia);
        using var db = _factory.CreateDbContext();
        Assert.Equal(2, db.Movimientos.Count());
        var saldo = db.SaldosCuenta.First(s => s.CuentaId == 1 && s.Moneda == "USD");
        Assert.Equal(110m, saldo.Saldo);
    }

    [Fact]
    public void RealizarArqueo_Faltante_CreaMovimientosYAjustaSaldo()
    {
        // Contamos 80 USD pero el sistema dice 100 → faltante de -20
        var result = _service.RealizarArqueoCiego(cuentaId: 1, moneda: "USD", montoContado: 80m);

        Assert.True(result.Exitoso);
        Assert.Equal(-20m, result.Diferencia);
        using var db = _factory.CreateDbContext();
        Assert.Equal(2, db.Movimientos.Count());
        var saldo = db.SaldosCuenta.First(s => s.CuentaId == 1 && s.Moneda == "USD");
        Assert.Equal(80m, saldo.Saldo);
    }

    [Fact]
    public void RealizarArqueo_Sobrante_CreaCtaDiferenciasSiNoExiste()
    {
        _service.RealizarArqueoCiego(cuentaId: 1, moneda: "USD", montoContado: 150m);

        using var db = _factory.CreateDbContext();
        var ctaDif = db.Cuentas.FirstOrDefault(c => c.Nombre == "Diferencias de Caja");
        Assert.NotNull(ctaDif);
    }

    [Fact]
    public void RealizarArqueo_ObservacionAutoGenerada_SegunDiferencia()
    {
        _service.RealizarArqueoCiego(cuentaId: 1, moneda: "USD", montoContado: 100m); // cuadra
        _service.RealizarArqueoCiego(cuentaId: 1, moneda: "USD", montoContado: 120m); // sobrante
        _service.RealizarArqueoCiego(cuentaId: 1, moneda: "USD", montoContado: 50m);  // faltante

        using var db = _factory.CreateDbContext();
        var arqueos = db.Arqueos.OrderBy(a => a.Id).ToList();
        Assert.Equal("Cuadra",   arqueos[0].Observaciones);
        Assert.Equal("Sobrante", arqueos[1].Observaciones);
        Assert.Equal("Faltante", arqueos[2].Observaciones);
    }
}
