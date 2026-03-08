using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SistemaCambio.ApiClient;
using CasaCambio.Shared.Requests;
using System;

namespace SistemaCambio.Views
{
    public partial class LoginWindow : Window
    {
        private readonly ICasaCambioApiClient _apiClient;
        private readonly AuthTokenStore _tokenStore;

        public bool LoginExitoso { get; private set; }

        public LoginWindow()
        {
            _apiClient = App.Services.GetRequiredService<ICasaCambioApiClient>();
            _tokenStore = App.Services.GetRequiredService<AuthTokenStore>();

            InitializeComponent();
            txtUsuario.Text = "";
        }

        private void TxtPassword_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnLogin_Click(sender, e);
        }

        private async void BtnLogin_Click(object? sender, RoutedEventArgs e)
        {
            var username = txtUsuario.Text?.Trim();
            var password = txtPassword.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MostrarError("Ingrese usuario y contrasena");
                return;
            }

            btnLogin.IsEnabled = false;
            txtError.IsVisible = false;
            txtStatus.Text = "Conectando...";

            try
            {
                var response = await _apiClient.LoginAsync(new LoginRequest
                {
                    Username = username,
                    Password = password
                });

                LoginExitoso = true;
                Close();
            }
            catch (System.Net.Http.HttpRequestException)
            {
                MostrarError("No se pudo conectar al servidor. Verifique que el servidor este ejecutandose.");
            }
            catch (Exception ex)
            {
                MostrarError($"Error de autenticacion: {ex.Message}");
            }
            finally
            {
                btnLogin.IsEnabled = true;
                txtStatus.Text = "";
            }
        }

        private void MostrarError(string mensaje)
        {
            txtError.Text = mensaje;
            txtError.IsVisible = true;
        }
    }
}
