using System;
using System.Net.Http;
using System.Threading.Tasks;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;

namespace SistemaCambio.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly ICasaCambioApiClient _apiClient;

    private string _username = "";
    private string _password = "";
    private string _errorMessage = "";
    private bool _isLoading;
    private bool _loginExitoso;

    public event Action? SolicitarCerrar;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(TieneError));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool LoginExitoso
    {
        get => _loginExitoso;
        set => SetProperty(ref _loginExitoso, value);
    }

    public bool TieneError => !string.IsNullOrEmpty(_errorMessage);

    public IAsyncRelayCommand LoginCommand { get; }

    public LoginViewModel(ICasaCambioApiClient apiClient)
    {
        _apiClient = apiClient;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
    }

    private async Task LoginAsync()
    {
        var username = Username?.Trim();
        var password = Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Ingrese usuario y contrasena";
            return;
        }

        IsLoading = true;
        ErrorMessage = "";

        try
        {
            var response = await _apiClient.LoginAsync(new LoginRequest
            {
                Username = username,
                Password = password
            });

            LoginExitoso = true;
            SolicitarCerrar?.Invoke();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo conectar al servidor. Verifique que el servidor este ejecutandose.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error de autenticacion: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
