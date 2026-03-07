using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaCambio.Models
{
    public class AppDbContext : DbContext
    {
        // Constructor sin parámetros para uso normal
        public AppDbContext() { }
        
        // Constructor con opciones para testing con InMemory DB
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Cuenta> Cuentas { get; set; }
        public DbSet<SaldoCuenta> SaldosCuenta { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Operacion> Operaciones { get; set; }
        public DbSet<Movimiento> Movimientos { get; set; }
        public DbSet<Moneda> Monedas { get; set; }
        public DbSet<CotizacionDiaria> CotizacionesDiarias { get; set; }
        public DbSet<Arqueo> Arqueos { get; set; }
        
        // Entidades de arquitectura
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<EstadoCaja> EstadosCaja { get; set; }
        public DbSet<TenenciaMoneda> TenenciasMoneda { get; set; }
        public DbSet<CierreCaja> CierresCaja { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Solo configurar PostgreSQL si no hay opciones ya configuradas (para testing)
            if (!options.IsConfigured)
            {
                options.UseNpgsql("Host=localhost;Database=SistemaCambio;Username=postgres;Password=19022006");

#if DEBUG
                // Solo en modo Debug: mostrar queries SQL generados
                // Útil para detectar N+1 y queries lentos
                options
                    .EnableSensitiveDataLogging()
                    .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
#endif
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configurar relación Operacion -> Cliente (opcional)
            modelBuilder.Entity<Operacion>()
                .HasOne(o => o.Cliente)
                .WithMany()
                .HasForeignKey(o => o.ClienteId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configurar relación Movimiento -> Operacion
            modelBuilder.Entity<Movimiento>()
                .HasOne(m => m.Operacion)
                .WithMany(o => o.Movimientos)
                .HasForeignKey(m => m.OperacionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relación Movimiento -> Cuenta
            modelBuilder.Entity<Movimiento>()
                .HasOne(m => m.Cuenta)
                .WithMany()
                .HasForeignKey(m => m.CuentaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configurar relación SaldoCuenta -> Cuenta
            modelBuilder.Entity<SaldoCuenta>()
                .HasOne(s => s.Cuenta)
                .WithMany(c => c.Saldos)
                .HasForeignKey(s => s.CuentaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índice único: una sola entrada por cuenta+moneda
            modelBuilder.Entity<SaldoCuenta>()
                .HasIndex(s => new { s.CuentaId, s.Moneda })
                .IsUnique();
        }
    }

    [Table("cuentas")]
    public class Cuenta
    {
        [Key] [Column("id")] public int Id { get; set; }
        [Column("nombre")] public string Nombre { get; set; } = "";
        [Column("tipo")] public string Tipo { get; set; } = "Caja";

        // Navegación: saldos por moneda
        public List<SaldoCuenta> Saldos { get; set; } = new();
    }

    [Table("saldos_cuenta")]
    public class SaldoCuenta
    {
        [Key] [Column("id")] public int Id { get; set; }
        [Column("cuenta_id")] public int CuentaId { get; set; }
        [Column("moneda")] public string Moneda { get; set; } = "USD";
        [Column("saldo")] public decimal Saldo { get; set; }

        public Cuenta Cuenta { get; set; } = null!;
    }
}