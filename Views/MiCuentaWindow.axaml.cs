using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.ViewModels;

namespace SistemaCambio.Views;

public partial class MiCuentaWindow : Window
{
    public bool CerroSesion { get; private set; }

    public MiCuentaWindow()
    {
        InitializeComponent();

        var vm = new MiCuentaViewModel(App.Services.GetRequiredService<ICasaCambioApiClient>());
        vm.SolicitarCerrar        += Close;
        vm.SolicitarCerrarSesion  += () => { CerroSesion = true; Close(); };
        DataContext = vm;

        Loaded += async (s, e) => await vm.CargarPerfilAsync();
    }
}
