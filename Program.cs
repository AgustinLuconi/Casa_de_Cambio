using Avalonia;
using System;
using Velopack;

namespace SistemaCambio;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // DEBE ser lo primero: Velopack intercepta los argumentos especiales que el
        // instalador/updater le pasa al ejecutable (install, first-run, update) y actúa
        // sin levantar la UI. En desarrollo (no instalado) simplemente continúa.
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
