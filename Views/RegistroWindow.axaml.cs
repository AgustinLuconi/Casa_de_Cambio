using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.ViewModels;

namespace SistemaCambio.Views;

public partial class RegistroWindow : Window
{
    public RegistroWindow()
    {
        InitializeComponent();

        var vm = new RegistroViewModel(App.Services.GetRequiredService<ICasaCambioApiClient>());
        vm.SolicitarCerrar += Close;
        DataContext = vm;
    }
}
