using CasaCambio.Server.Services;
using Xunit;

namespace CasaCambio.Tests;

public class AuditServiceTests
{
    private readonly IAuditService _auditService;
    private readonly TestDbContextFactory _factory;

    public AuditServiceTests()
    {
        _factory = new TestDbContextFactory();
        _auditService = new AuditService(_factory);
    }

    [Fact]
    public void Registrar_DeberiaEjecutarseSinExcepciones()
    {
        var exception = Record.Exception(() =>
        {
            _auditService.Registrar("TEST", "Prueba", 1, datosNuevos: new { mensaje = "test" });
        });
        Assert.Null(exception);
    }

    [Fact]
    public void Registrar_DeberiaGuardarEnBaseDeDatos()
    {
        _auditService.Registrar("CREATE", "Operacion", 1, datosNuevos: new { tipo = "Compra" });

        using var db = _factory.CreateDbContext();
        var log = db.AuditLogs.FirstOrDefault();
        Assert.NotNull(log);
        Assert.Equal("CREATE", log.Accion);
        Assert.Equal("Operacion", log.Entidad);
        Assert.Equal(1, log.EntidadId);
    }

    [Fact]
    public void Registrar_ConDatosNulos_NoDeberiaFallar()
    {
        var exception = Record.Exception(() =>
        {
            _auditService.Registrar("CREATE", "Test", 1);
        });
        Assert.Null(exception);
    }

    [Fact]
    public void RegistrarCambioCotizacion_NoDeberiaFallar()
    {
        var exception = Record.Exception(() =>
        {
            _auditService.RegistrarCambioCotizacion(1, 1000m, 1050m);
        });
        Assert.Null(exception);
    }

    [Fact]
    public void RegistrarEliminacion_NoDeberiaFallar()
    {
        var exception = Record.Exception(() =>
        {
            _auditService.RegistrarEliminacion("Operacion", 123, new { tipo = "Compra" });
        });
        Assert.Null(exception);
    }
}
