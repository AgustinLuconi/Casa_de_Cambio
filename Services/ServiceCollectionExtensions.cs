using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.LocalDb;
using SistemaCambio.Services.Offline;

namespace SistemaCambio.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigurarServicios(this IServiceCollection services, string apiBaseUrl = "https://casa-cambio-api.fly.dev")
        {
            // Auth token store (singleton, shared across all services)
            services.AddSingleton<AuthTokenStore>();

            // API Client via HttpClientFactory
            services.AddHttpClient<ICasaCambioApiClient, CasaCambioApiClient>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
                client.Timeout = AppConstants.HttpTimeout;
            });

            // SQLite local database
            services.AddDbContextFactory<LocalDbContext>(options =>
                options.UseSqlite($"Data Source={LocalDbContext.GetDefaultDbPath()}"));

            // Offline services
            services.AddSingleton<ConnectivityChecker>();
            services.AddSingleton<OfflineOperacionService>();
            services.AddHostedService<SyncService>();

            return services;
        }
    }
}
