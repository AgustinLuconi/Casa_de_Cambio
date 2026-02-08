using Microsoft.EntityFrameworkCore;
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
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Operacion> Operaciones { get; set; }
        public DbSet<Movimiento> Movimientos { get; set; }
        public DbSet<Moneda> Monedas { get; set; }
        public DbSet<CotizacionDiaria> CotizacionesDiarias { get; set; }
        public DbSet<Arqueo> Arqueos { get; set; }
        
        // Nuevas entidades para mejoras de arquitectura
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<EstadoCaja> EstadosCaja { get; set; }
        public DbSet<TenenciaMoneda> TenenciasMoneda { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Solo configurar PostgreSQL si no hay opciones ya configuradas (para testing)
            if (!options.IsConfigured)
            {
                options.UseNpgsql("Host=localhost;Database=SistemaCambio;Username=postgres;Password=19022006");
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
        }
    }

    [Table("cuentas")]
    public class Cuenta
    {
        [Key] [Column("id")] public int Id { get; set; }
        [Column("nombre")] public string Nombre { get; set; } = "";
        [Column("tipo")] public string Tipo { get; set; } = "Caja"; 
        [Column("moneda")] public string Moneda { get; set; } = "USD";
        [Column("saldo")] public decimal Saldo { get; set; }
    }
}