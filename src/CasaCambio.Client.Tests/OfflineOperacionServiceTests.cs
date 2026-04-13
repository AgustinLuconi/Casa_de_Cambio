using CasaCambio.Shared.Enums;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.Responses;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using SistemaCambio.ApiClient;
using SistemaCambio.LocalDb;
using SistemaCambio.Services.Offline;
using Xunit;

namespace CasaCambio.Client.Tests;

public class OfflineOperacionServiceTests : IDisposable
{
    private readonly Mock<ICasaCambioApiClient> _mockApiClient;
    private readonly Mock<IConnectivityChecker> _mockConnectivity;
    private readonly TestLocalDbContextFactory _localDbFactory;
    private readonly OfflineOperacionService _service;

    public OfflineOperacionServiceTests()
    {
        _mockApiClient = new Mock<ICasaCambioApiClient>();
        _mockConnectivity = new Mock<IConnectivityChecker>();

        _localDbFactory = new TestLocalDbContextFactory();
        using var db = _localDbFactory.CreateDbContext();
        db.Database.EnsureCreated();

        _service = new OfflineOperacionService(
            _mockApiClient.Object,
            _localDbFactory,
            _mockConnectivity.Object);
    }

    public void Dispose() => _localDbFactory.Dispose();

    private static CrearOperacionRequest CrearRequestCompra() => new()
    {
        CuentaOrigenId = 1, CuentaDestinoId = 2,
        MonedaOrigen = "ARS", MonedaDestino = "USD",
        MontoOrigen = 1000m, MontoDestino = 1m, Cotizacion = 1000m
    };

    [Fact]
    public async Task GuardarCompra_Online_LlamaApiYRetornaExitoso()
    {
        _mockConnectivity.Setup(x => x.CheckAsync()).ReturnsAsync(true);
        _mockApiClient.Setup(x => x.CrearCompraAsync(It.IsAny<CrearOperacionRequest>()))
            .ReturnsAsync(OperacionResponse.Success(42));

        var result = await _service.GuardarCompraAsync(CrearRequestCompra());

        Assert.True(result.Exitoso);
        Assert.False(result.IsOffline);
        Assert.Equal(42, result.OperacionId);
    }

    [Fact]
    public async Task GuardarCompra_Offline_GuardaEnSQLiteYRetornaExitoso()
    {
        _mockConnectivity.Setup(x => x.CheckAsync()).ReturnsAsync(false);

        var result = await _service.GuardarCompraAsync(CrearRequestCompra());

        Assert.True(result.Exitoso);
        Assert.True(result.IsOffline);
        Assert.NotNull(result.LocalId);
    }

    [Fact]
    public async Task GuardarCompra_Offline_GuardaConEstadoPendiente()
    {
        _mockConnectivity.Setup(x => x.CheckAsync()).ReturnsAsync(false);

        await _service.GuardarCompraAsync(CrearRequestCompra());

        using var db = _localDbFactory.CreateDbContext();
        var pendiente = db.OperacionesPendientes.First();
        Assert.Equal(EstadoSincronizacion.Pendiente, pendiente.EstadoSync);
        Assert.Equal("Compra", pendiente.TipoOperacion);
    }

    [Fact]
    public async Task GuardarCompra_OnlineConErrorDeRed_GuardaOffline()
    {
        _mockConnectivity.Setup(x => x.CheckAsync()).ReturnsAsync(true);
        _mockApiClient.Setup(x => x.CrearCompraAsync(It.IsAny<CrearOperacionRequest>()))
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var result = await _service.GuardarCompraAsync(CrearRequestCompra());

        Assert.True(result.Exitoso);
        Assert.True(result.IsOffline);
    }

    [Fact]
    public async Task ObtenerPendientesCount_SinPendientes_RetornaCero()
    {
        var count = await _service.ObtenerPendientesCountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ObtenerPendientesCount_ConPendientes_RetornaCorrecto()
    {
        _mockConnectivity.Setup(x => x.CheckAsync()).ReturnsAsync(false);
        await _service.GuardarCompraAsync(CrearRequestCompra());
        await _service.GuardarVentaAsync(CrearRequestCompra());

        var count = await _service.ObtenerPendientesCountAsync();

        Assert.Equal(2, count);
    }
}

/// <summary>
/// Factory que mantiene una conexión SQLite in-memory abierta para que la base de datos
/// persista entre las múltiples llamadas a CreateDbContext() dentro de un test.
/// </summary>
public class TestLocalDbContextFactory : IDbContextFactory<LocalDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LocalDbContext> _options;

    public TestLocalDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<LocalDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public LocalDbContext CreateDbContext() => new LocalDbContext(_options);

    public void Dispose() => _connection.Dispose();
}
