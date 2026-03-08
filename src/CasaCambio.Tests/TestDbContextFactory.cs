using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using CasaCambio.Server.Data;

namespace CasaCambio.Tests;

/// <summary>
/// Helper que crea un IDbContextFactory respaldado por InMemory database.
/// Cada test usa su propia base de datos aislada.
/// Configura InMemory para ignorar TransactionIgnoredWarning.
/// </summary>
public class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(string? dbName = null)
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? $"TestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    public AppDbContext CreateDbContext() => new AppDbContext(_options);
}
