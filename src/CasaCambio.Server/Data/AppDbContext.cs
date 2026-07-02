using Microsoft.EntityFrameworkCore;
using CasaCambio.Server.Models;

namespace CasaCambio.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Cuenta> Cuentas { get; set; }
    public DbSet<SaldoCuenta> SaldosCuenta { get; set; }
    public DbSet<Operacion> Operaciones { get; set; }
    public DbSet<Movimiento> Movimientos { get; set; }
    public DbSet<Moneda> Monedas { get; set; }
    public DbSet<CotizacionDiaria> CotizacionesDiarias { get; set; }
    public DbSet<Arqueo> Arqueos { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<EstadoCaja> EstadosCaja { get; set; }
    public DbSet<TenenciaMoneda> TenenciasMoneda { get; set; }
    public DbSet<CierreCaja> CierresCaja { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<ConfiguracionSistema> ConfiguracionSistema { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movimiento>()
            .HasOne(m => m.Operacion)
            .WithMany(o => o.Movimientos)
            .HasForeignKey(m => m.OperacionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Movimiento>()
            .HasOne(m => m.Cuenta)
            .WithMany()
            .HasForeignKey(m => m.CuentaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SaldoCuenta>()
            .HasOne(s => s.Cuenta)
            .WithMany(c => c.Saldos)
            .HasForeignKey(s => s.CuentaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SaldoCuenta>()
            .HasIndex(s => new { s.CuentaId, s.Moneda })
            .IsUnique();

        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<Operacion>().HasIndex(o => o.Fecha);
        modelBuilder.Entity<Operacion>().HasIndex(o => new { o.TipoOperacion, o.Fecha });
        modelBuilder.Entity<Operacion>()
            .HasIndex(o => o.IdempotencyKey)
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL");
        modelBuilder.Entity<Movimiento>().HasIndex(m => new { m.CuentaId, m.Fecha });
        modelBuilder.Entity<Movimiento>().HasIndex(m => m.Fecha);
        modelBuilder.Entity<CierreCaja>().HasIndex(c => c.Fecha);
        modelBuilder.Entity<Arqueo>().HasIndex(a => a.Fecha);
    }
}
