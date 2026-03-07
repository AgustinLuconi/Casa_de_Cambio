using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;

namespace SistemaCambio.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigurarServicios(this IServiceCollection services)
        {
            // DbContext Factory — centraliza la configuración de la conexión
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseNpgsql("Host=localhost;Database=SistemaCambio;Username=postgres;Password=19022006");

#if DEBUG
                options
                    .EnableSensitiveDataLogging()
                    .LogTo(System.Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
#endif
            });

            // Servicios de negocio (Singleton porque son stateless, solo usan factory)
            services.AddSingleton<IAuditService, AuditService>();
            services.AddSingleton<ICierreCajaService, CierreCajaService>();
            services.AddSingleton<IOperacionService, OperacionService>();
            services.AddSingleton<IPPPService, PPPService>();
            services.AddSingleton<IArqueoService, ArqueoService>();
            services.AddSingleton<IDashboardService, DashboardService>();
            services.AddSingleton<IQueryService, QueryService>();

            // Validadores
            services.AddSingleton<Validators.OperacionValidator>();
            services.AddSingleton<Validators.ArqueoValidator>();

            return services;
        }
    }
}
