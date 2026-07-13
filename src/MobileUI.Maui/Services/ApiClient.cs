using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using StrategyTradingAppUI.Maui.Models;

namespace StrategyTradingAppUI.Maui.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private string _baseUrl;
    private string _expectedCertificateThumbprint;
    private string _apiKey;
    private const string BaseUrlPrefsKey = "api_base_url";
    private const string ThumbprintPrefsKey = "api_cert_thumbprint";
    private const string ApiKeyPrefsKey = "api_key";
    private const string DefaultBaseUrl = "http://localhost:5000";
    private const string DefaultThumbprint = "";

    public ApiClient()
    {
        _baseUrl = Preferences.Get(BaseUrlPrefsKey, DefaultBaseUrl);
        _expectedCertificateThumbprint = Preferences.Get(ThumbprintPrefsKey, DefaultThumbprint);
        _apiKey = SecureStorage.GetAsync(ApiKeyPrefsKey).GetAwaiter().GetResult() ?? "";

        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = ValidateServerCertificate;

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        UpdateDefaultHeaders();
    }

    private bool ValidateServerCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        System.Net.Security.SslPolicyErrors errors)
    {
        if (certificate == null)
            return false;

        var thumbprint = certificate.Thumbprint.ToUpperInvariant();

        // If thumbprint is empty/default, allow any certificate (development only)
        if (string.IsNullOrEmpty(_expectedCertificateThumbprint))
            return true;

        // Otherwise, thumbprint must match
        if (thumbprint != _expectedCertificateThumbprint)
            return false;

        // Accept self-signed cert errors as long as thumbprint matches
        return true;
    }

    public void SetBaseUrl(string url)
    {
        _baseUrl = url.TrimEnd('/');
        Preferences.Set(BaseUrlPrefsKey, _baseUrl);
    }

    public void SetCertificateThumbprint(string thumbprint)
    {
        _expectedCertificateThumbprint = thumbprint.ToUpperInvariant();
        Preferences.Set(ThumbprintPrefsKey, _expectedCertificateThumbprint);
    }

    public string GetBaseUrl() => _baseUrl;

    public async Task SetApiKeyAsync(string apiKey)
    {
        _apiKey = apiKey;
        await SecureStorage.SetAsync(ApiKeyPrefsKey, apiKey);
        UpdateDefaultHeaders();
    }

    public async Task<string> GetApiKeyAsync()
    {
        return await SecureStorage.GetAsync(ApiKeyPrefsKey) ?? "";
    }

    private void UpdateDefaultHeaders()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        }
    }

    public async Task<List<Position>> GetPositionsAsync()
    {
        var positions = await _httpClient.GetFromJsonAsync<List<Position>>($"{_baseUrl}/api/positions");
        return positions ?? new List<Position>();
    }

    public async Task<List<TradeRecord>> GetRecentTradesAsync(int count = 20)
    {
        var trades = await _httpClient.GetFromJsonAsync<List<TradeRecord>>($"{_baseUrl}/api/trades/recent?count={count}");
        return trades ?? new List<TradeRecord>();
    }

    public async Task<DaemonStatus> GetHealthAsync()
    {
        var status = await _httpClient.GetFromJsonAsync<DaemonStatus>($"{_baseUrl}/api/health");
        return status ?? new DaemonStatus { DaemonRunning = false };
    }

    public async Task<CommandResponse> SellAsync(string ticker)
    {
        var request = new { ticker };
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/trades/sell", request);
        if (!response.IsSuccessStatusCode)
            return new CommandResponse { Status = "error", Message = $"HTTP {(int)response.StatusCode}" };
        var result = await response.Content.ReadFromJsonAsync<CommandResponse>();
        return result ?? new CommandResponse { Status = "error", Message = "Failed to parse response" };
    }

    public async Task<CommandResponse> SellAllAsync()
    {
        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/trades/sell-all", content);
        if (!response.IsSuccessStatusCode)
            return new CommandResponse { Status = "error", Message = $"HTTP {(int)response.StatusCode}" };
        var result = await response.Content.ReadFromJsonAsync<CommandResponse>();
        return result ?? new CommandResponse { Status = "error", Message = "Failed to parse response" };
    }

    public async Task<CommandResponse> PauseBuyingAsync()
    {
        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/trades/pause-buying", content);
        if (!response.IsSuccessStatusCode)
            return new CommandResponse { Status = "error", Message = $"HTTP {(int)response.StatusCode}" };
        var result = await response.Content.ReadFromJsonAsync<CommandResponse>();
        return result ?? new CommandResponse { Status = "error", Message = "Failed to parse response" };
    }

    public async Task<CommandResponse> ResumeBuyingAsync()
    {
        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/trades/resume-buying", content);
        if (!response.IsSuccessStatusCode)
            return new CommandResponse { Status = "error", Message = $"HTTP {(int)response.StatusCode}" };
        var result = await response.Content.ReadFromJsonAsync<CommandResponse>();
        return result ?? new CommandResponse { Status = "error", Message = "Failed to parse response" };
    }

    public async Task<List<TradeCommand>> GetCommandsAsync()
    {
        var commands = await _httpClient.GetFromJsonAsync<List<TradeCommand>>($"{_baseUrl}/api/trades/commands");
        return commands ?? new List<TradeCommand>();
    }

    public async Task<TradeCommand?> GetCommandAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<TradeCommand>($"{_baseUrl}/api/trades/commands/{id}");
    }

    public async Task<string?> CancelCommandAsync(string id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/trades/commands/{id}");
            if (response.IsSuccessStatusCode)
                return null;

            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.Conflict => "Command is already executing",
                System.Net.HttpStatusCode.NotFound => "Command not found",
                _ => $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
