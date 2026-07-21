using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using SistemaCambio.ViewModels;

namespace SistemaCambio.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public bool LoginExitoso { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();

            _viewModel = new LoginViewModel(App.Services.GetRequiredService<ICasaCambioApiClient>());
            _viewModel.SolicitarCerrar += () =>
            {
                LoginExitoso = _viewModel.LoginExitoso;
                Close();
            };
            DataContext = _viewModel;
        }

        private void TxtPassword_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.LoginCommand.CanExecute(null))
                _viewModel.LoginCommand.Execute(null);
        }

        private async void BtnRecuperar_Click(object? sender, RoutedEventArgs e)
        {
            await new RecuperarPasswordWindow().ShowDialog(this);
        }

        private async void BtnRegistro_Click(object? sender, RoutedEventArgs e)
        {
            await new RegistroWindow().ShowDialog(this);
        }
    }
}
