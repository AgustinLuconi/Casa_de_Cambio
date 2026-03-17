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
