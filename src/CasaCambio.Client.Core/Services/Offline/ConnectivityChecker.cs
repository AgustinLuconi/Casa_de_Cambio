using System;
using System.Threading;
using System.Threading.Tasks;
using SistemaCambio.ApiClient;

namespace SistemaCambio.Services.Offline;

public class ConnectivityChecker : IConnectivityChecker, IDisposable
{
    private readonly ICasaCambioApiClient _apiClient;
    private readonly Timer _timer;
    private bool _isOnline;
    private bool _disposed;

    public bool IsOnline => _isOnline;
    public event Action<bool>? OnConnectivityChanged;

    // Constructor de producción — usa el intervalo de AppConstants
    public ConnectivityChecker(ICasaCambioApiClient apiClient)
        : this(apiClient, AppConstants.IntervaloVerificacionConectividad) { }

    // Constructor testeable — permite inyectar un intervalo arbitrario
    public ConnectivityChecker(ICasaCambioApiClient apiClient, TimeSpan intervalo)
    {
        _apiClient = apiClient;
        _timer = new Timer(async _ => await CheckAsync(), null, TimeSpan.Zero, intervalo);
    }

    public async Task<bool> CheckAsync()
    {
        try
        {
            var online = await _apiClient.HealthCheckAsync();
            if (online != _isOnline)
            {
                _isOnline = online;
                OnConnectivityChanged?.Invoke(_isOnline);
            }
            return _isOnline;
        }
        catch (Exception ex)
        {
            AppLogger.Warn("ConnectivityChecker.CheckAsync", ex);
            if (_isOnline)
            {
                _isOnline = false;
                OnConnectivityChanged?.Invoke(false);
            }
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
