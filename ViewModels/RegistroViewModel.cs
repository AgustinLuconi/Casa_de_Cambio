using System;
using System.Threading.Tasks;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;

namespace SistemaCambio.ViewModels;

public class RegistroViewModel : ViewModelBase
{
    private readonly ICasaCambioApiClient _apiClient;

    private string _nombreCompleto = "";
    private string _email = "";
    private string _password = "";
    private string _confirmarPassword = "";
    private string _errorMessage = "";
    private bool _isLoading;
    private bool _registroExitoso;

    public event Action? SolicitarCerrar;

    public string NombreCompleto
    {
        get => _nombreCompleto;
        set => SetProperty(ref _nombreCompleto, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmarPassword
    {
        get => _confirmarPassword;
        set => SetProperty(ref _confirmarPassword, value);
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

    public bool RegistroExitoso
    {
        get => _registroExitoso;
        set => SetProperty(ref _registroExitoso, value);
    }

    public bool TieneError => !string.IsNullOrEmpty(_errorMessage);

    public IAsyncRelayCommand RegistrarCommand { get; }
    public IRelayCommand VolverAlLoginCommand { get; }

    public RegistroViewModel(ICasaCambioApiClient apiClient)
    {
        _apiClient = apiClient;
        RegistrarCommand = new AsyncRelayCommand(RegistrarAsync);
        VolverAlLoginCommand = new RelayCommand(() => SolicitarCerrar?.Invoke());
    }

    private async Task RegistrarAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreCompleto) || string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmarPassword))
        {
            ErrorMessage = "Todos los campos son obligatorios.";
            return;
        }
        if (Password != ConfirmarPassword)
        {
            ErrorMessage = "Las contrase\u00f1as no coinciden.";
            return;
        }
        if (Password.Length < 8)
        {
            ErrorMessage = "La contrase\u00f1a debe tener al menos 8 caracteres.";
            return;
        }

        IsLoading = true;
        ErrorMessage = "";

        try
        {
            var response = await _apiClient.RegistrarUsuarioAsync(new RegisterRequest
            {
                NombreCompleto = NombreCompleto,
                Email = Email,
                Password = Password,
                ConfirmarPassword = ConfirmarPassword
            });

            if (response.Exitoso)
                RegistroExitoso = true;
            else
                ErrorMessage = response.Mensaje;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
