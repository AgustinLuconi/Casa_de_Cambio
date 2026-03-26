using System;
using System.Net.Mail;
using System.Threading.Tasks;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;

namespace SistemaCambio.ViewModels;

public class RecuperarPasswordViewModel : ViewModelBase
{
    private readonly ICasaCambioApiClient _apiClient;

    private string _email = "";
    private string _codigo = "";
    private string _nuevaPassword = "";
    private string _confirmarPassword = "";
    private string _errorMessage = "";
    private bool _isLoading;
    private int _pasoActual = 1;

    public event Action? SolicitarCerrar;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
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

    public int PasoActual
    {
        get => _pasoActual;
        set
        {
            if (SetProperty(ref _pasoActual, value))
            {
                OnPropertyChanged(nameof(EsPaso1));
                OnPropertyChanged(nameof(EsPaso2));
                OnPropertyChanged(nameof(EsPaso3));
                OnPropertyChanged(nameof(EsPaso4));
            }
        }
    }

    public string Codigo            { get => _codigo;            set => SetProperty(ref _codigo, value); }
    public string NuevaPassword     { get => _nuevaPassword;     set => SetProperty(ref _nuevaPassword, value); }
    public string ConfirmarPassword { get => _confirmarPassword; set => SetProperty(ref _confirmarPassword, value); }

    public bool TieneError => !string.IsNullOrEmpty(_errorMessage);
    public bool EsPaso1 => _pasoActual == 1;
    public bool EsPaso2 => _pasoActual == 2;
    public bool EsPaso3 => _pasoActual == 3;
    public bool EsPaso4 => _pasoActual == 4;

    public IAsyncRelayCommand EnviarEmailCommand      { get; }
    public IAsyncRelayCommand ResetearPasswordCommand  { get; }
    public IRelayCommand      IrAlPaso3Command         { get; }
    public IRelayCommand      VolverAlLoginCommand     { get; }

    public RecuperarPasswordViewModel(ICasaCambioApiClient apiClient)
    {
        _apiClient = apiClient;
        EnviarEmailCommand     = new AsyncRelayCommand(EnviarEmailAsync);
        ResetearPasswordCommand = new AsyncRelayCommand(ResetearPasswordAsync);
        IrAlPaso3Command       = new RelayCommand(() => PasoActual = 3);
        VolverAlLoginCommand   = new RelayCommand(() => SolicitarCerrar?.Invoke());
    }

    private async Task EnviarEmailAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Ingres\u00e1 tu email.";
            return;
        }
        try { _ = new MailAddress(Email); }
        catch
        {
            ErrorMessage = "El formato del email no es v\u00e1lido.";
            return;
        }

        IsLoading = true;
        ErrorMessage = "";

        try
        {
            await _apiClient.RecuperarPasswordAsync(new RecuperarPasswordRequest { Email = Email });
        }
        catch { /* error de red — igual transitamos al paso 2 */ }
        finally
        {
            IsLoading = false;
        }

        PasoActual = 2;
    }

    private async Task ResetearPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(Codigo))
        { ErrorMessage = "Ingres\u00e1 el c\u00f3digo del email."; return; }
        if (string.IsNullOrWhiteSpace(NuevaPassword) || NuevaPassword.Length < 8)
        { ErrorMessage = "La contrase\u00f1a debe tener al menos 8 caracteres."; return; }
        if (NuevaPassword != ConfirmarPassword)
        { ErrorMessage = "Las contrase\u00f1as no coinciden."; return; }

        IsLoading = true;
        ErrorMessage = "";
        try
        {
            var result = await _apiClient.ResetearPasswordAsync(new ResetearPasswordRequest
            {
                Token             = Codigo.Trim().ToUpper(),
                NuevaPassword     = NuevaPassword,
                ConfirmarPassword = ConfirmarPassword
            });
            if (result.Exitoso) PasoActual = 4;
            else                ErrorMessage = result.Mensaje;
        }
        finally { IsLoading = false; }
    }
}
