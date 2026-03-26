using System;
using System.Threading.Tasks;
using CasaCambio.Shared.Requests;
using CommunityToolkit.Mvvm.Input;
using SistemaCambio.ApiClient;

namespace SistemaCambio.ViewModels;

public class MiCuentaViewModel : ViewModelBase
{
    private readonly ICasaCambioApiClient _apiClient;

    private string _nombreCompleto = "";
    private string _email = "";
    private string _username = "";
    private string _rol = "";
    private DateTime _fechaCreacion;
    private bool _emailConfirmado;
    private bool _isLoading;
    private bool _isLoadingReenvio;
    private string _exitoReenvio = "";
    private string _errorPerfil = "";
    private string _errorPassword = "";
    private string _exitoPerfil = "";
    private string _exitoPassword = "";
    private string _passwordActual = "";
    private string _nuevaPassword = "";
    private string _confirmarPassword = "";

    public event Action? SolicitarCerrar;

    public string NombreCompleto { get => _nombreCompleto; set => SetProperty(ref _nombreCompleto, value); }
    public string Email          { get => _email;          set => SetProperty(ref _email, value); }
    public string Username       { get => _username;       set => SetProperty(ref _username, value); }
    public string Rol            { get => _rol;            set { if (SetProperty(ref _rol, value)) OnPropertyChanged(nameof(EsAdmin)); } }
    public DateTime FechaCreacion { get => _fechaCreacion; set { if (SetProperty(ref _fechaCreacion, value)) OnPropertyChanged(nameof(FechaCreacionFormateada)); } }
    public bool EmailConfirmado  { get => _emailConfirmado;  set => SetProperty(ref _emailConfirmado, value); }
    public bool IsLoading        { get => _isLoading;        set => SetProperty(ref _isLoading, value); }
    public bool IsLoadingReenvio { get => _isLoadingReenvio; set => SetProperty(ref _isLoadingReenvio, value); }
    public string ExitoReenvio   { get => _exitoReenvio;     set { if (SetProperty(ref _exitoReenvio, value)) OnPropertyChanged(nameof(TieneExitoReenvio)); } }
    public bool TieneExitoReenvio => !string.IsNullOrEmpty(_exitoReenvio);

    public string ErrorPerfil   { get => _errorPerfil;   set { if (SetProperty(ref _errorPerfil, value))   OnPropertyChanged(nameof(TieneErrorPerfil)); } }
    public string ErrorPassword { get => _errorPassword; set { if (SetProperty(ref _errorPassword, value)) OnPropertyChanged(nameof(TieneErrorPassword)); } }
    public string ExitoPerfil   { get => _exitoPerfil;   set { if (SetProperty(ref _exitoPerfil, value))   OnPropertyChanged(nameof(TieneExitoPerfil)); } }
    public string ExitoPassword { get => _exitoPassword; set { if (SetProperty(ref _exitoPassword, value)) OnPropertyChanged(nameof(TieneExitoPassword)); } }

    public string PasswordActual    { get => _passwordActual;    set => SetProperty(ref _passwordActual, value); }
    public string NuevaPassword     { get => _nuevaPassword;     set => SetProperty(ref _nuevaPassword, value); }
    public string ConfirmarPassword { get => _confirmarPassword; set => SetProperty(ref _confirmarPassword, value); }

    public bool TieneErrorPerfil   => !string.IsNullOrEmpty(_errorPerfil);
    public bool TieneErrorPassword => !string.IsNullOrEmpty(_errorPassword);
    public bool TieneExitoPerfil   => !string.IsNullOrEmpty(_exitoPerfil);
    public bool TieneExitoPassword => !string.IsNullOrEmpty(_exitoPassword);
    public bool EsAdmin => _rol == "Admin";
    public string FechaCreacionFormateada => _fechaCreacion == default ? "" : _fechaCreacion.ToLocalTime().ToString("dd/MM/yyyy");

    public IAsyncRelayCommand CargarPerfilCommand          { get; }
    public IAsyncRelayCommand GuardarPerfilCommand         { get; }
    public IAsyncRelayCommand CambiarPasswordCommand       { get; }
    public IAsyncRelayCommand ReenviarConfirmacionCommand  { get; }
    public IRelayCommand      CerrarCommand                { get; }

    public MiCuentaViewModel(ICasaCambioApiClient apiClient)
    {
        _apiClient = apiClient;
        CargarPerfilCommand         = new AsyncRelayCommand(CargarPerfilAsync);
        GuardarPerfilCommand        = new AsyncRelayCommand(GuardarPerfilAsync);
        CambiarPasswordCommand      = new AsyncRelayCommand(CambiarPasswordAsync);
        ReenviarConfirmacionCommand = new AsyncRelayCommand(ReenviarConfirmacionAsync);
        CerrarCommand               = new RelayCommand(() => SolicitarCerrar?.Invoke());
    }

    public async Task CargarPerfilAsync()
    {
        IsLoading = true;
        try
        {
            var perfil = await _apiClient.ObtenerPerfilAsync();
            if (perfil != null)
            {
                NombreCompleto = perfil.NombreCompleto;
                Email          = perfil.Email;
                Username       = perfil.Username;
                Rol            = perfil.Rol;
                FechaCreacion   = perfil.FechaCreacion;
                EmailConfirmado = perfil.EmailConfirmado;
            }
            else
            {
                ErrorPerfil = "No se pudo cargar el perfil. Verific\u00e1 tu conexi\u00f3n.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GuardarPerfilAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreCompleto))
        {
            ErrorPerfil = "El nombre no puede estar vac\u00edo.";
            return;
        }
        ErrorPerfil = "";
        IsLoading = true;
        try
        {
            var ok = await _apiClient.ActualizarPerfilAsync(new ActualizarPerfilRequest
            {
                NombreCompleto = NombreCompleto,
                Email = Email
            });
            if (ok) { ExitoPerfil = "Perfil actualizado."; _ = LimpiarExitoAsync(true); }
            else    { ErrorPerfil = "No se pudo actualizar el perfil."; }
        }
        finally { IsLoading = false; }
    }

    private async Task CambiarPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(PasswordActual) || string.IsNullOrWhiteSpace(NuevaPassword))
        { ErrorPassword = "Complet\u00e1 todos los campos."; return; }
        if (NuevaPassword != ConfirmarPassword)
        { ErrorPassword = "Las contrase\u00f1as no coinciden."; return; }
        if (NuevaPassword.Length < 8)
        { ErrorPassword = "La contrase\u00f1a debe tener al menos 8 caracteres."; return; }

        ErrorPassword = "";
        IsLoading = true;
        try
        {
            var ok = await _apiClient.CambiarPasswordAsync(new CambiarPasswordRequest
            {
                PasswordActual    = PasswordActual,
                NuevaPassword     = NuevaPassword,
                ConfirmarPassword = ConfirmarPassword
            });
            if (ok)
            {
                ExitoPassword     = "Contrase\u00f1a actualizada.";
                PasswordActual    = "";
                NuevaPassword     = "";
                ConfirmarPassword = "";
                _ = LimpiarExitoAsync(false);
            }
            else { ErrorPassword = "Contrase\u00f1a actual incorrecta o error del servidor."; }
        }
        finally { IsLoading = false; }
    }

    private async Task LimpiarExitoAsync(bool esPerfil)
    {
        await Task.Delay(3000);
        if (esPerfil) ExitoPerfil = "";
        else          ExitoPassword = "";
    }

    private async Task ReenviarConfirmacionAsync()
    {
        IsLoadingReenvio = true;
        ErrorPerfil = "";
        try
        {
            var result = await _apiClient.ReenviarConfirmacionAsync();
            if (result.Exitoso) { ExitoReenvio = result.Mensaje; _ = LimpiarExitoReenvioAsync(); }
            else                { ErrorPerfil = result.Mensaje; }
        }
        finally { IsLoadingReenvio = false; }
    }

    private async Task LimpiarExitoReenvioAsync()
    {
        await Task.Delay(4000);
        ExitoReenvio = "";
    }
}
