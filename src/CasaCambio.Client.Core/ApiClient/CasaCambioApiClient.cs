using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CasaCambio.Shared.DTOs;
using CasaCambio.Shared.Requests;
using CasaCambio.Shared.Responses;

namespace SistemaCambio.ApiClient;

public class CasaCambioApiClient : ICasaCambioApiClient
{
    private readonly HttpClient _http;
    private readonly AuthTokenStore _tokenStore;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public event Action? OnSessionExpired;

    public CasaCambioApiClient(HttpClient http, AuthTokenStore tokenStore)
    {
        _http = http;
        _tokenStore = tokenStore;
    }

    // Auth

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", request);
        
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
                throw new Exception(error?.Message ?? "Usuario o contraseña incorrectos.");
            }
            response.EnsureSuccessStatusCode(); 
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _tokenStore.SetTokens(auth!);
        return auth!;
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        var response = await _http.PostAsJsonAsync("api/auth/refresh", new { RefreshToken = refreshToken });
        if (!response.IsSuccessStatusCode) return null;
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        if (auth != null) _tokenStore.SetTokens(auth);
        return auth;
    }

    public async Task LogoutAsync()
    {
        try { await PostAuthenticatedAsync<object>("api/auth/logout", new { }); }
        catch { /* si el server falla, igual limpiamos localmente */ }
        finally { _tokenStore.Clear(); }
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/auth/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RecuperarPasswordAsync(RecuperarPasswordRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/recuperar", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UsuarioPerfilDto?> ObtenerPerfilAsync()
    {
        try { return await GetAuthenticatedAsync<UsuarioPerfilDto>("api/auth/me"); }
        catch { return null; }
    }

    public async Task<bool> ActualizarPerfilAsync(ActualizarPerfilRequest request)
    {
        try { await PutAuthenticatedAsync<UsuarioPerfilDto>("api/auth/me", request); return true; }
        catch { return false; }
    }

    public async Task<bool> CambiarPasswordAsync(CambiarPasswordRequest request)
    {
        try { await PutAuthenticatedAsync<object>("api/auth/cambiar-password", request); return true; }
        catch { return false; }
    }

    public async Task<RegisterResponse> ReenviarConfirmacionAsync()
    {
        try
        {
            return await PostAuthenticatedAsync<RegisterResponse>("api/auth/reenviar-confirmacion", new { });
        }
        catch (Exception ex)
        {
            return new RegisterResponse { Exitoso = false, Mensaje = ex.Message };
        }
    }

    public async Task<RegisterResponse> ResetearPasswordAsync(ResetearPasswordRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/resetear", request);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions))!;
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
            return new RegisterResponse { Exitoso = false, Mensaje = error?.Message ?? "Error al resetear." };
        }
        catch (Exception ex)
        {
            return new RegisterResponse { Exitoso = false, Mensaje = ex.Message };
        }
    }

    public async Task<RegisterResponse> RegistrarUsuarioAsync(RegisterRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/register", request);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions))!;

            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
            return new RegisterResponse { Exitoso = false, Mensaje = error?.Message ?? "Error al registrar." };
        }
        catch (Exception ex)
        {
            return new RegisterResponse { Exitoso = false, Mensaje = ex.Message };
        }
    }

    // Operaciones

    public async Task<OperacionResponse> CrearCompraAsync(CrearOperacionRequest request)
        => await PostAuthenticatedAsync<OperacionResponse>("api/operaciones/compra", request);

    public async Task<OperacionResponse> CrearVentaAsync(CrearOperacionRequest request)
        => await PostAuthenticatedAsync<OperacionResponse>("api/operaciones/venta", request);

    public async Task<OperacionResponse> CrearCreditoDebitoAsync(CrearCreditoDebitoRequest request)
        => await PostAuthenticatedAsync<OperacionResponse>("api/operaciones/credito-debito", request);

    public async Task<OperacionResponse> CrearInterbancarioAsync(CrearInterbancarioRequest request)
        => await PostAuthenticatedAsync<OperacionResponse>("api/operaciones/interbancaria", request);

    public async Task<PaginatedResponse<OperacionDto>> ObtenerOperacionesAsync(DateTime? desde = null, DateTime? hasta = null, string? tipo = null, int page = 1, int pageSize = 50)
    {
        var query = $"api/operaciones?page={page}&pageSize={pageSize}";
        if (desde.HasValue) query += $"&desde={desde.Value:O}";
        if (hasta.HasValue) query += $"&hasta={hasta.Value:O}";
        if (!string.IsNullOrEmpty(tipo)) query += $"&tipo={Uri.EscapeDataString(tipo)}";
        return await GetAuthenticatedAsync<PaginatedResponse<OperacionDto>>(query);
    }

    public async Task<OperacionDto?> ObtenerOperacionAsync(int id)
    {
        try { return await GetAuthenticatedAsync<OperacionDto>($"api/operaciones/{id}"); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    public async Task<OperacionResponse> AnularOperacionAsync(int id)
        => await PostAuthenticatedAsync<OperacionResponse>($"api/operaciones/{id}/anular", new { });

    // Cuentas

    public async Task<List<CuentaDto>> ObtenerCuentasAsync()
        => await GetAuthenticatedAsync<List<CuentaDto>>("api/cuentas");

    public async Task<CuentaDto> CrearCuentaAsync(CrearCuentaRequest request)
        => await PostAuthenticatedAsync<CuentaDto>("api/cuentas", request);

    public async Task<CuentaDto> ActualizarCuentaAsync(int cuentaId, CrearCuentaRequest request)
        => await PutAuthenticatedAsync<CuentaDto>($"api/cuentas/{cuentaId}", request);

    public async Task EliminarCuentaAsync(int cuentaId)
        => await DeleteAuthenticatedAsync($"api/cuentas/{cuentaId}");

    public async Task<PaginatedResponse<MovimientoDto>> ObtenerMovimientosCuentaAsync(int cuentaId, DateTime? desde = null, DateTime? hasta = null, int page = 1, int pageSize = 200)
    {
        var query = $"api/cuentas/{cuentaId}/movimientos?page={page}&pageSize={pageSize}";
        if (desde.HasValue) query += $"&desde={desde.Value:O}";
        if (hasta.HasValue) query += $"&hasta={hasta.Value:O}";
        return await GetAuthenticatedAsync<PaginatedResponse<MovimientoDto>>(query);
    }

    public async Task<List<SaldoCuentaDto>> ObtenerSaldosCuentaAsync(int cuentaId)
        => await GetAuthenticatedAsync<List<SaldoCuentaDto>>($"api/cuentas/{cuentaId}/saldos");

    public async Task<bool> ObtenerEstadoDiaCerradoAsync()
    {
        try
        {
            var result = await GetAuthenticatedAsync<JsonElement>("api/cuentas/estado-dia");
            return result.TryGetProperty("cerrado", out var prop) && prop.GetBoolean();
        }
        catch { return false; }
    }

    // Monedas

    public async Task<List<MonedaDto>> ObtenerMonedasAsync()
        => await GetAuthenticatedAsync<List<MonedaDto>>("api/monedas");

    public async Task<MonedaDto> CrearMonedaAsync(CrearMonedaRequest request)
        => await PostAuthenticatedAsync<MonedaDto>("api/monedas", request);

    public async Task<MonedaDto> ActualizarMonedaAsync(int id, ActualizarMonedaRequest request)
        => await PutAuthenticatedAsync<MonedaDto>($"api/monedas/{id}", request);

    public async Task EliminarMonedaAsync(int id)
        => await DeleteAuthenticatedAsync($"api/monedas/{id}");

    // Cotizaciones

    public async Task<List<CotizacionDto>> ObtenerCotizacionesHoyAsync()
        => await GetAuthenticatedAsync<List<CotizacionDto>>("api/cotizaciones/hoy");

    public async Task GuardarCotizacionAsync(CrearCotizacionRequest request)
        => await PostAuthenticatedAsync<object>("api/cotizaciones", request);

    // Clientes

    public async Task<List<ClienteDto>> ObtenerClientesAsync()
        => await GetAuthenticatedAsync<List<ClienteDto>>("api/clientes");

    // Arqueo

    public async Task<ArqueoDto> RealizarArqueoAsync(CrearArqueoRequest request)
        => await PostAuthenticatedAsync<ArqueoDto>("api/arqueo", request);

    // Cierre de Caja

    public async Task<CierreCajaDto?> ObtenerCierreHoyAsync()
    {
        try { return await GetAuthenticatedAsync<CierreCajaDto>("api/cierre/hoy"); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    public async Task<CierreCajaDto> GenerarCierreAsync(string observaciones = "")
        => await PostAuthenticatedAsync<CierreCajaDto>("api/cierre/generar", new { Observaciones = observaciones });

    public async Task<CierreCajaDto> CerrarDefinitivoAsync(int id)
        => await PostAuthenticatedAsync<CierreCajaDto>($"api/cierre/{id}/cerrar", new { });

    // Configuración

    public async Task<string?> ObtenerConfiguracionAsync(string clave)
    {
        try
        {
            var result = await GetAuthenticatedAsync<JsonElement>($"api/configuracion/{Uri.EscapeDataString(clave)}");
            return result.GetProperty("valor").GetString();
        }
        catch { return null; }
    }

    public async Task<bool> ActualizarConfiguracionAsync(string clave, string valor)
    {
        try
        {
            await PutAuthenticatedAsync<JsonElement>($"api/configuracion/{Uri.EscapeDataString(clave)}", new { Valor = valor });
            return true;
        }
        catch { return false; }
    }

    // PPP

    public async Task<decimal> ObtenerPPPAsync(string moneda)
    {
        var result = await GetAuthenticatedAsync<JsonElement>($"api/ppp/{Uri.EscapeDataString(moneda)}");
        return result.GetProperty("ppp").GetDecimal();
    }

    public async Task<PPPValidacionDto> ValidarVentaPPPAsync(string moneda, decimal cotizacion)
        => await GetAuthenticatedAsync<PPPValidacionDto>($"api/ppp/{Uri.EscapeDataString(moneda)}/validar-venta?cotizacion={cotizacion}");

    // Dashboard

    public async Task<DashboardDto> ObtenerDashboardAsync()
        => await GetAuthenticatedAsync<DashboardDto>("api/dashboard");

    // Sync

    public async Task<SyncPushResponse> SyncPushAsync(SyncPushRequest request)
        => await PostAuthenticatedAsync<SyncPushResponse>("api/sync/push", request);

    public async Task<SyncPullResponse> SyncPullAsync()
        => await GetAuthenticatedAsync<SyncPullResponse>("api/sync/pull");

    // Internal helpers

    private async Task ThrowIfNotSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        try
        {
            var error = JsonSerializer.Deserialize<ApiErrorResponse>(body, JsonOptions);
            if (!string.IsNullOrEmpty(error?.Message))
                throw new Exception(error.Message);
        }
        catch (JsonException) { }
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim('"'),
            null, response.StatusCode);
    }

    private async Task<T> GetAuthenticatedAsync<T>(string url)
    {
        await EnsureAuthenticatedAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

        var response = await _http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (await TryRefreshAsync())
            {
                request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
                response = await _http.SendAsync(request);
            }
            else
            {
                OnSessionExpired?.Invoke();
                throw new UnauthorizedAccessException("Sesion expirada");
            }
        }

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task<T> PostAuthenticatedAsync<T>(string url, object body)
    {
        await EnsureAuthenticatedAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (await TryRefreshAsync())
            {
                request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
                request.Content = JsonContent.Create(body);
                response = await _http.SendAsync(request);
            }
            else
            {
                OnSessionExpired?.Invoke();
                throw new UnauthorizedAccessException("Sesion expirada");
            }
        }

        await ThrowIfNotSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task<T> PutAuthenticatedAsync<T>(string url, object body)
    {
        await EnsureAuthenticatedAsync();
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (await TryRefreshAsync())
            {
                request = new HttpRequestMessage(HttpMethod.Put, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
                request.Content = JsonContent.Create(body);
                response = await _http.SendAsync(request);
            }
            else
            {
                OnSessionExpired?.Invoke();
                throw new UnauthorizedAccessException("Sesion expirada");
            }
        }

        await ThrowIfNotSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task DeleteAuthenticatedAsync(string url)
    {
        await EnsureAuthenticatedAsync();
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

        var response = await _http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (await TryRefreshAsync())
            {
                request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
                response = await _http.SendAsync(request);
            }
            else
            {
                OnSessionExpired?.Invoke();
                throw new UnauthorizedAccessException("Sesion expirada");
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var msg = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim('"');
            throw new HttpRequestException(msg, null, response.StatusCode);
        }
    }

    private async Task EnsureAuthenticatedAsync()
    {
        if (!_tokenStore.IsAuthenticated)
            throw new UnauthorizedAccessException("No autenticado. Debe hacer login primero.");

        if (_tokenStore.NeedsRefresh)
            await TryRefreshAsync();
    }

    private async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(_tokenStore.RefreshToken)) return false;

        await _refreshLock.WaitAsync();
        try
        {
            if (!_tokenStore.NeedsRefresh) return true;
            var result = await RefreshTokenAsync(_tokenStore.RefreshToken);
            return result != null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
