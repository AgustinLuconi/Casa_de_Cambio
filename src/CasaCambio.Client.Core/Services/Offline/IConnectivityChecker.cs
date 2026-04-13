namespace SistemaCambio.Services.Offline;

public interface IConnectivityChecker
{
    bool IsOnline { get; }
    Task<bool> CheckAsync();
    event Action<bool>? OnConnectivityChanged;
}
