using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;
using t12Project.Contracts.Auth;

namespace t12Project.Services;

public sealed class AuthClient
{
    private const string StorageKey = "auth.tokens";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NavigationManager _navigationManager;
    private readonly ProtectedLocalStorage _storage;
    private readonly ILogger<AuthClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AuthClient(
        IHttpClientFactory httpClientFactory,
        NavigationManager navigationManager,
        ProtectedLocalStorage storage,
        ILogger<AuthClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _navigationManager = navigationManager;
        _storage = storage;
        _logger = logger;
    }

    public async Task<AuthClientResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== AuthClient.RegisterAsync START ===");
        _logger.LogInformation("Registering user: {Email}, FullName: {FullName}, Role: {Role}", request.Email, request.FullName, request.Role);
        
        try
        {
            var client = CreateClient();
            _logger.LogInformation("Sending POST to api/auth/register...");
            
            var response = await client.PostAsJsonAsync("api/auth/register", request, cancellationToken);
            _logger.LogInformation("Response status: {StatusCode}", response.StatusCode);
            
            if (!response.IsSuccessStatusCode)
            {
                var (message, isBlocked) = await ReadErrorMessageAsync(response, cancellationToken);
                _logger.LogWarning("Registration failed with status {StatusCode}: {Message}", response.StatusCode, message);
                return AuthClientResult.Failure(message, isBlocked);
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions, cancellationToken);
            if (auth is null)
            {
                _logger.LogWarning("Failed to parse auth response");
                return AuthClientResult.Failure("Unable to parse authentication response.");
            }

            _logger.LogInformation("Registration successful for {Email}, storing auth...", request.Email);
            await StoreAuthAsync(auth);
            _logger.LogInformation("=== AuthClient.RegisterAsync SUCCESS ===");
            return AuthClientResult.Success(auth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== AuthClient.RegisterAsync EXCEPTION ===");
            return AuthClientResult.Failure("Unable to register right now. Please try again.");
        }
    }

    public async Task<AuthClientResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== AuthClient.LoginAsync START ===");
        _logger.LogInformation("Attempting login for: {Email}", request.Email);
        
        try
        {
            var client = CreateClient();
            _logger.LogInformation("Base URI: {BaseUri}", client.BaseAddress);
            _logger.LogInformation("Sending POST to api/auth/login...");
            
            var response = await client.PostAsJsonAsync("api/auth/login", request, cancellationToken);
            _logger.LogInformation("Response status: {StatusCode}", response.StatusCode);
            
            if (!response.IsSuccessStatusCode)
            {
                var (message, isBlocked) = await ReadErrorMessageAsync(response, cancellationToken);
                _logger.LogWarning("Login failed with status {StatusCode}: {Message}, Blocked: {IsBlocked}", response.StatusCode, message, isBlocked);
                return AuthClientResult.Failure(message, isBlocked);
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions, cancellationToken);
            if (auth is null)
            {
                _logger.LogWarning("Failed to parse auth response");
                return AuthClientResult.Failure("Unable to parse authentication response.");
            }

            _logger.LogInformation("Login successful for {Email}, storing auth...", request.Email);
            await StoreAuthAsync(auth);
            _logger.LogInformation("=== AuthClient.LoginAsync SUCCESS ===");
            return AuthClientResult.Success(auth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== AuthClient.LoginAsync EXCEPTION ===");
            return AuthClientResult.Failure($"Unable to sign in: {ex.Message}");
        }
    }

    public async Task<AuthClientResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredAuthAsync();
        if (stored is null)
        {
            return AuthClientResult.Failure("No refresh token available.");
        }

        try
        {
            var client = CreateClient();
            var payload = new RefreshRequest { RefreshToken = stored.RefreshToken };
            var response = await client.PostAsJsonAsync("api/auth/refresh", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var (message, isBlocked) = await ReadErrorMessageAsync(response, cancellationToken);
                return AuthClientResult.Failure(message, isBlocked);
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions, cancellationToken);
            if (auth is null)
            {
                return AuthClientResult.Failure("Unable to refresh session.");
            }

            await StoreAuthAsync(auth);
            return AuthClientResult.Success(auth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh token request failed");
            return AuthClientResult.Failure("Unable to refresh session.");
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredAuthAsync();
        if (stored is not null)
        {
            try
            {
                var client = CreateClient();
                var payload = new RefreshRequest { RefreshToken = stored.RefreshToken };
                await client.PostAsJsonAsync("api/auth/logout", payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Logout request failed");
            }
        }

        await ClearAuthAsync();
    }

    public async Task StoreAuthAsync(AuthResponse response)
    {
        await _storage.SetAsync(StorageKey, response);
    }

    public async Task<AuthResponse?> GetStoredAuthAsync()
    {
        var result = await _storage.GetAsync<AuthResponse>(StorageKey);
        return result.Success ? result.Value : null;
    }

    public async Task ClearAuthAsync()
    {
        await _storage.DeleteAsync(StorageKey);
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri(_navigationManager.BaseUri);
        }

        return client;
    }

    private static async Task<(string Message, bool IsBlocked)> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? message = null;
        bool isBlocked = false;
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(content))
            {
                using var document = JsonDocument.Parse(content);
                if (document.RootElement.TryGetProperty("message", out var msgProp))
                {
                    message = msgProp.GetString();
                }
                else if (document.RootElement.ValueKind == JsonValueKind.String)
                {
                    message = document.RootElement.GetString();
                }

                // Check if user is blocked
                if (document.RootElement.TryGetProperty("blocked", out var blockedProp))
                {
                    isBlocked = blockedProp.GetBoolean();
                }
            }
        }
        catch
        {
            // ignored
        }

        message ??= $"Request failed with status {(int)response.StatusCode}";
        return (message, isBlocked);
    }
}

public sealed record AuthClientResult(bool Succeeded, string Message, AuthResponse? Payload, bool IsBlocked = false)
{
    public static AuthClientResult Success(AuthResponse payload) => new(true, string.Empty, payload);
    public static AuthClientResult Failure(string message, bool isBlocked = false) => new(false, message, null, isBlocked);
}
