using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.Models;
using SistemaCambio.Services;
using SistemaCambio.ViewModels;
using SistemaCambio.Views;

namespace SistemaCambio;

public partial class App : Application
{
    /// <summary>
    /// Contenedor de servicios global — accesible desde toda la app.
    /// Las vistas lo usan para resolver sus dependencias.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            DisableAvaloniaDataAnnotationValidation();

            // Configurar contenedor de DI
            var serviceCollection = new ServiceCollection();
            serviceCollection.ConfigurarServicios();
            Services = serviceCollection.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    Services.GetRequiredService<IDbContextFactory<AppDbContext>>()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}