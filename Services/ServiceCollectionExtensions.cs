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
            // Auth token store (Singleton — comparte estado de autenticación en toda la app)
            services.AddSingleton<AuthTokenStore>();

            // API Client via HttpClientFactory (Transient gestionado por la factory)
            services.AddHttpClient<ICasaCambioApiClient, CasaCambioApiClient>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
                client.Timeout = AppConstants.HttpTimeout;
            });

            // SQLite local database
            services.AddDbContextFactory<LocalDbContext>(options =>
                options.UseSqlite($"Data Source={LocalDbContext.GetDefaultDbPath()}"));

            // ConnectivityChecker: Singleton con Timer interno — registrado por su interfaz
            services.AddSingleton<IConnectivityChecker, ConnectivityChecker>();

            // OfflineOperacionService: factory delegate que resuelve el captive dependency.
            // Usamos IHttpClientFactory (Singleton) para crear el HttpClient en vez de
            // capturar ICasaCambioApiClient (Transient) directamente en el Singleton.
            services.AddSingleton<IOfflineOperacionService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
                var tokenStore = sp.GetRequiredService<AuthTokenStore>();
                // AddHttpClient<TClient,TImpl> registra bajo typeof(TClient).Name, no typeof(TImpl).Name
                var httpClient = httpClientFactory.CreateClient(typeof(ICasaCambioApiClient).Name);
                var apiClient = new CasaCambioApiClient(httpClient, tokenStore);
                var localDbFactory = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
                var connectivity = sp.GetRequiredService<IConnectivityChecker>();
                return new OfflineOperacionService(apiClient, localDbFactory, connectivity);
            });

            // SyncService registrado como Singleton Y como HostedService:
            // — Singleton: permite resolver App.Services.GetRequiredService<SyncService>() para suscribir eventos
            // — HostedService: arranca el BackgroundService al iniciar la app
            services.AddSingleton<SyncService>();
            services.AddHostedService(sp => sp.GetRequiredService<SyncService>());

            return services;
        }
    }
}
