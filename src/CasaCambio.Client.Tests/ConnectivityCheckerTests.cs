using Moq;
using SistemaCambio.ApiClient;
using SistemaCambio.Services.Offline;
using Xunit;

namespace CasaCambio.Client.Tests;

public class ConnectivityCheckerTests : IDisposable
{
    private readonly Mock<ICasaCambioApiClient> _mockApiClient;
    private readonly ConnectivityChecker _checker;

    public ConnectivityCheckerTests()
    {
        _mockApiClient = new Mock<ICasaCambioApiClient>();
        // Intervalo de 1 hora para que el timer nunca dispare automáticamente durante los tests
        _checker = new ConnectivityChecker(_mockApiClient.Object, TimeSpan.FromHours(1));
    }

    public void Dispose() => _checker.Dispose();

    [Fact]
    public async Task CheckAsync_CuandoApiResponde_RetornaTrue()
    {
        _mockApiClient.Setup(x => x.HealthCheckAsync()).ReturnsAsync(true);

        var result = await _checker.CheckAsync();

        Assert.True(result);
        Assert.True(_checker.IsOnline);
    }

    [Fact]
    public async Task CheckAsync_CuandoApiNoResponde_RetornaFalse()
    {
        _mockApiClient.Setup(x => x.HealthCheckAsync()).ReturnsAsync(false);

        var result = await _checker.CheckAsync();

        Assert.False(result);
        Assert.False(_checker.IsOnline);
    }

    [Fact]
    public async Task CheckAsync_CuandoApiLanzaExcepcion_RetornaFalse()
    {
        _mockApiClient.Setup(x => x.HealthCheckAsync())
            .ThrowsAsync(new HttpRequestException("Sin conexión"));

        var result = await _checker.CheckAsync();

        Assert.False(result);
        Assert.False(_checker.IsOnline);
    }

    [Fact]
    public async Task CheckAsync_CuandoCambiaDeOfflineAOnline_DispararaEvento()
    {
        // Establece estado inicial: offline
        _mockApiClient.Setup(x => x.HealthCheckAsync()).ReturnsAsync(false);
        await _checker.CheckAsync();

        bool? eventoRecibido = null;
        _checker.OnConnectivityChanged += estado => eventoRecibido = estado;

        // Ahora cambia a online
        _mockApiClient.Setup(x => x.HealthCheckAsync()).ReturnsAsync(true);
        await _checker.CheckAsync();

        Assert.True(eventoRecibido);
    }

    [Fact]
    public async Task CheckAsync_CuandoEstadoNoCambia_NoDisparaEvento()
    {
        // Establece estado inicial: online
        _mockApiClient.Setup(x => x.HealthCheckAsync()).ReturnsAsync(true);
        await _checker.CheckAsync();

        int eventosDisparados = 0;
        _checker.OnConnectivityChanged += _ => eventosDisparados++;

        // Sigue online — no debe disparar el evento
        await _checker.CheckAsync();

        Assert.Equal(0, eventosDisparados);
    }
}
