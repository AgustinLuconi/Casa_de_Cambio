using System;
using System.Threading;
using System.Threading.Tasks;
using SistemaCambio.ApiClient;

namespace SistemaCambio.Services.Offline;

public class ConnectivityChecker : IDisposable
{
    private readonly ICasaCambioApiClient _apiClient;
    private readonly Timer _timer;
    private bool _isOnline;
    private bool _disposed;

    public bool IsOnline => _isOnline;
    public event Action<bool>? OnConnectivityChanged;

    public ConnectivityChecker(ICasaCambioApiClient apiClient)
    {
        _apiClient = apiClient;
        _timer = new Timer(async _ => await CheckAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
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
        catch
        {
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
