using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.ViewModels;

namespace SistemaCambio.Views;

public partial class RecuperarPasswordWindow : Window
{
    public RecuperarPasswordWindow()
    {
        InitializeComponent();

        var vm = new RecuperarPasswordViewModel(App.Services.GetRequiredService<ICasaCambioApiClient>());
        vm.SolicitarCerrar += Close;
        DataContext = vm;
    }
}
