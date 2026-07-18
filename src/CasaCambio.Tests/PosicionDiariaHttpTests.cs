using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CasaCambio.Server.Auth;
using CasaCambio.Server.Data;
using CasaCambio.Server.Models;
using CasaCambio.Shared.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasaCambio.Tests;

/// <summary>
/// Verifica el endpoint por HTTP real (no llamando al controller directo), para atajar la
/// categoría de bug de ruteo que los tests in-memory no pueden ver (ver commit 4c1660d:
/// [Route("api/[controller]")] resolvía a "api/PosicionDiaria" sin guión, distinto de la
/// ruta "api/posicion-diaria" que llama el cliente desktop).
/// </summary>
public class PosicionDiariaHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PosicionDiariaHttpTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // El SecretKey de appsettings.json es un placeholder corto ("CONFIGURAR_VIA_FLY_SECRETS",
            // reemplazado en producción por un secret de Fly.io) — muy corto para HMAC-SHA256 (mínimo 256 bits).
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:SecretKey"] = "ClaveDePruebaSoloParaTests_MinimoTreintaYDosBytes"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextFactory<AppDbContext>>();
                services.AddSingleton<IDbContextFactory<AppDbContext>>(
                    new TestDbContextFactory($"HttpTestDb_{Guid.NewGuid()}"));
            });
        });
    }

    [Fact]
    public async Task GetPosicionDiaria_RutaRealHTTP_Responde200ConDatosCorrectos()
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = dbFactory.CreateDbContext())
        {
            db.Monedas.Add(new Moneda { Id = 1, Codigo = "USD", Nombre = "Dólar", Activa = true, TipoPase = "D" });
            var cajaEfectivo = new Cuenta { Id = 1, Nombre = "EFECTIVO USD", Tipo = "Efectivo" };
            db.Cuentas.Add(cajaEfectivo);
            var op = new Operacion { TipoOperacion = "Compra", MontoTotalOrigen = 1m, MontoTotalDestino = 1m, CotizacionAplicada = 1m };
            db.Operaciones.Add(op);
            db.Movimientos.Add(new Movimiento { Operacion = op, CuentaId = 1, Moneda = "USD", Monto = 1000m, Fecha = new DateTime(2026, 6, 1) });
            db.SaveChanges();
        }

        var jwtService = scope.ServiceProvider.GetRequiredService<JwtService>();
        var usuarioDePrueba = new Usuario { Id = 1, Username = "test", Rol = "Admin", NombreCompleto = "Test" };
        var token = jwtService.GenerarToken(usuarioDePrueba);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("api/posicion-diaria?desde=2026-06-10T00:00:00Z&hasta=2026-06-20T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var posiciones = await response.Content.ReadFromJsonAsync<List<PosicionDiariaDto>>();
        Assert.NotNull(posiciones);
        var usd = Assert.Single(posiciones!, p => p.Codigo == "USD");
        Assert.Equal(1000m, usd.CapInicial);
    }

    [Fact]
    public async Task GetPosicionDiaria_SinToken_Responde401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("api/posicion-diaria?desde=2026-06-10T00:00:00Z&hasta=2026-06-20T00:00:00Z");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
