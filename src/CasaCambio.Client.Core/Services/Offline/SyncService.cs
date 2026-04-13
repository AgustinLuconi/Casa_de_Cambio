using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CasaCambio.Shared.Enums;
using CasaCambio.Shared.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SistemaCambio.ApiClient;
using SistemaCambio.LocalDb;

namespace SistemaCambio.Services.Offline;

public class SyncService : BackgroundService
{
    private readonly ICasaCambioApiClient _apiClient;
    private readonly IDbContextFactory<LocalDbContext> _localDbFactory;
    private readonly IConnectivityChecker _connectivity;
    private readonly AuthTokenStore _tokenStore;

    public event Action<int>? OnSyncCompleted;
    public event Action<string>? OnSyncError;

    public SyncService(
        ICasaCambioApiClient apiClient,
        IDbContextFactory<LocalDbContext> localDbFactory,
        IConnectivityChecker connectivity,
        AuthTokenStore tokenStore)
    {
        _apiClient = apiClient;
        _localDbFactory = localDbFactory;
        _connectivity = connectivity;
        _tokenStore = tokenStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(AppConstants.IntervaloSincronizacion, stoppingToken);
                if (!_connectivity.IsOnline || !_tokenStore.IsAuthenticated) continue;
                await SyncPendingAsync();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                OnSyncError?.Invoke(ex.Message);
            }
        }
    }

    public async Task SyncPendingAsync()
    {
        using var db = _localDbFactory.CreateDbContext();

        var pendientes = await db.OperacionesPendientes
            .Where(o => (o.EstadoSync == EstadoSincronizacion.Pendiente || o.EstadoSync == EstadoSincronizacion.Error)
                        && o.IntentosSyncCount < AppConstants.SyncMaxRetries)
            .OrderBy(o => o.FechaCreacionLocal)
            .Take(AppConstants.SyncBatchSize)
            .ToListAsync();

        if (pendientes.Count == 0) return;

        foreach (var op in pendientes)
            op.EstadoSync = EstadoSincronizacion.Enviando;
        await db.SaveChangesAsync();

        var request = new SyncPushRequest
        {
            Operaciones = pendientes.Select(op => new OperacionOfflineRequest
            {
                LocalId = op.Id,
                TipoOperacion = op.TipoOperacion,
                CuentaOrigenId = op.CuentaOrigenId,
                CuentaDestinoId = op.CuentaDestinoId,
                MonedaOrigen = op.MonedaOrigen,
                MonedaDestino = op.MonedaDestino,
                MontoOrigen = op.MontoOrigen,
                MontoDestino = op.MontoDestino,
                CotizacionAplicada = op.CotizacionAplicada,
                ClienteId = op.ClienteId,
                Observaciones = op.Observaciones,
                FechaCreacionLocal = op.FechaCreacionLocal
            }).ToList()
        };

        try
        {
            var response = await _apiClient.SyncPushAsync(request);
            int synced = 0;
            foreach (var resultado in response.Resultados)
            {
                var op = pendientes.FirstOrDefault(p => p.Id == resultado.LocalId);
                if (op == null) continue;

                if (resultado.Exitoso)
                {
                    op.EstadoSync = EstadoSincronizacion.Sincronizado;
                    op.ServerOperacionId = resultado.ServerOperacionId;
                    op.FechaSincronizacion = DateTime.UtcNow;
                    synced++;
                }
                else
                {
                    op.EstadoSync = EstadoSincronizacion.Error;
                    op.ErrorSync = resultado.Mensaje;
                    op.IntentosSyncCount++;
                    if (op.IntentosSyncCount >= AppConstants.SyncMaxRetries)
                        op.EstadoSync = EstadoSincronizacion.RequiereRevision;
                }
            }
            await db.SaveChangesAsync();

            if (synced > 0)
            {
                await RefreshCacheAsync(db);
                OnSyncCompleted?.Invoke(synced);
            }
        }
        catch (Exception ex)
        {
            foreach (var op in pendientes)
            {
                op.EstadoSync = EstadoSincronizacion.Error;
                op.ErrorSync = ex.Message;
                op.IntentosSyncCount++;
            }
            await db.SaveChangesAsync();
            OnSyncError?.Invoke(ex.Message);
        }
    }

    private async Task RefreshCacheAsync(LocalDbContext db)
    {
        try
        {
            var pullData = await _apiClient.SyncPullAsync();

            db.CacheSaldos.RemoveRange(db.CacheSaldos);
            db.CacheCuentas.RemoveRange(db.CacheCuentas);
            foreach (var cuenta in pullData.Cuentas)
            {
                var cached = new CacheCuenta { Id = cuenta.Id, Nombre = cuenta.Nombre, Tipo = cuenta.Tipo };
                db.CacheCuentas.Add(cached);
                foreach (var saldo in cuenta.Saldos)
                    db.CacheSaldos.Add(new CacheSaldo { CuentaId = cuenta.Id, Moneda = saldo.Moneda, Saldo = saldo.Saldo });
            }

            db.CacheMonedas.RemoveRange(db.CacheMonedas);
            foreach (var moneda in pullData.Monedas)
                db.CacheMonedas.Add(new CacheMoneda { Id = moneda.Id, Codigo = moneda.Codigo, Nombre = moneda.Nombre, Activa = moneda.Activa });

            db.CacheCotizaciones.RemoveRange(db.CacheCotizaciones);
            foreach (var cot in pullData.Cotizaciones)
                db.CacheCotizaciones.Add(new CacheCotizacion { CodigoMoneda = cot.CodigoMoneda, Fecha = cot.Fecha, CotizacionCompra = cot.CotizacionCompra, CotizacionVenta = cot.CotizacionVenta });

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Warn("RefreshCacheAsync", ex);
        }
    }
}
