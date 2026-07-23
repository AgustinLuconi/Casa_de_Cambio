using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.LocalDb;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
using SistemaCambio.Views;

namespace SistemaCambio;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var serviceCollection = new ServiceCollection();
            serviceCollection.ConfigurarServicios();
            Services = serviceCollection.BuildServiceProvider();

            // Initialize local SQLite database
            var localDbFactory = Services.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            using (var localDb = localDbFactory.CreateDbContext())
            {
                localDb.Database.EnsureCreated();

                // Migración defensiva: instalaciones existentes con un local.db viejo (creado antes de
                // agregar los campos de Arbitraje) no tienen estas columnas, ya que EnsureCreated() solo
                // crea la base si no existe, no la migra. Se ignora el error si la columna ya existe
                // (caso normal en instalaciones nuevas, donde EnsureCreated() ya las creó).
                foreach (var alterSql in new[]
                {
                    "ALTER TABLE operaciones_pendientes ADD COLUMN CuentaDebitaVentaId INTEGER NOT NULL DEFAULT 0",
                    "ALTER TABLE operaciones_pendientes ADD COLUMN MonedaVenta TEXT NOT NULL DEFAULT ''",
                    "ALTER TABLE operaciones_pendientes ADD COLUMN MontoExtranjeroVenta TEXT NOT NULL DEFAULT '0'",
                    "ALTER TABLE operaciones_pendientes ADD COLUMN CotizacionVenta TEXT NOT NULL DEFAULT '0'",
                    "ALTER TABLE operaciones_pendientes ADD COLUMN TipoOperacionArbitraje TEXT NOT NULL DEFAULT ''"
                })
                {
                    try { localDb.Database.ExecuteSqlRaw(alterSql); }
                    catch { /* la columna ya existe (instalación nueva vía EnsureCreated) */ }
                }
            }

            // Prevent app from shutting down when LoginWindow closes
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            // Show login window first
            var loginWindow = new LoginWindow();
            loginWindow.Closed += (s, e) =>
            {
                if (loginWindow.LoginExitoso)
                {
                    var apiClient = Services.GetRequiredService<ICasaCambioApiClient>();
                    var mainWindow = new MainWindow
                    {
                        DataContext = new MainWindowViewModel(apiClient),
                    };
                    desktop.MainWindow = mainWindow;
                    desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                    mainWindow.Show();
                }
                else
                {
                    desktop.Shutdown();
                }
            };
            desktop.MainWindow = loginWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
