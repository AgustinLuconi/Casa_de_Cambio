using System;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace SistemaCambio.LocalDb;

public class LocalDbContext : DbContext
{
    public DbSet<LocalOperacion> OperacionesPendientes { get; set; } = null!;
    public DbSet<CacheCuenta> CacheCuentas { get; set; } = null!;
    public DbSet<CacheSaldo> CacheSaldos { get; set; } = null!;
    public DbSet<CacheMoneda> CacheMonedas { get; set; } = null!;
    public DbSet<CacheCotizacion> CacheCotizaciones { get; set; } = null!;
    public DbSet<SyncMetadata> SyncMetadata { get; set; } = null!;
    public DbSet<AuthSession> AuthSessions { get; set; } = null!;

    public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options) { }

    public static string GetDefaultDbPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "CasaCambio");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "local.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalOperacion>(entity =>
        {
            entity.ToTable("operaciones_pendientes");
            entity.HasIndex(e => e.EstadoSync);
            entity.HasIndex(e => e.FechaCreacionLocal);
        });

        modelBuilder.Entity<CacheCuenta>(entity =>
        {
            entity.ToTable("cache_cuentas");
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<CacheSaldo>(entity =>
        {
            entity.ToTable("cache_saldos");
            entity.HasOne(s => s.Cuenta).WithMany(c => c.Saldos).HasForeignKey(s => s.CuentaId);
        });

        modelBuilder.Entity<CacheMoneda>(entity =>
        {
            entity.ToTable("cache_monedas");
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<CacheCotizacion>(entity =>
        {
            entity.ToTable("cache_cotizaciones");
        });

        modelBuilder.Entity<SyncMetadata>(entity =>
        {
            entity.ToTable("sync_metadata");
        });

        modelBuilder.Entity<AuthSession>(entity =>
        {
            entity.ToTable("auth_session");
            entity.Property(e => e.Id).ValueGeneratedNever();
        });
    }
}
