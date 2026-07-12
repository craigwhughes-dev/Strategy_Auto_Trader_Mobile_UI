using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using MobileUI.Maui.Models;

namespace MobileUI.Maui.Services;

public interface IApiClient
{
    Task<List<Position>> GetPositionsAsync();
    Task<List<TradeRecord>> GetRecentTradesAsync(int count = 20);
    Task<DaemonStatus> GetHealthAsync();
    void SetBaseUrl(string url);
    string GetBaseUrl();
}

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private string _baseUrl = "http://192.168.1.100:5000";
    private const string ExpectedCertificateThumbprint = "7618F28C90EE396840E9B980773F8A69147E86CC";

    public ApiClient()
    {
        var handler = new HttpClientHandler();

        if (_baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            handler.ServerCertificateCustomValidationCallback = ValidateServerCertificate;
        }

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private bool ValidateServerCertificate(HttpRequestMessage request, X509Certificate2? certificate, X509Chain? chain, System.Net.Security.SslPolicyErrors errors)
    {
        if (certificate == null)
            return false;

        if (errors != System.Net.Security.SslPolicyErrors.None)
            return false;

        var thumbprint = certificate.Thumbprint.ToUpperInvariant();
        return thumbprint == ExpectedCertificateThumbprint;
    }

    public void SetBaseUrl(string url)
    {
        _baseUrl = url.TrimEnd('/');
    }

    public string GetBaseUrl() => _baseUrl;

    public async Task<List<Position>> GetPositionsAsync()
    {
        try
        {
            var positions = await _httpClient.GetFromJsonAsync<List<Position>>($"{_baseUrl}/api/positions");
            return positions ?? new List<Position>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching positions: {ex.Message}");
            return new List<Position>();
        }
    }

    public async Task<List<TradeRecord>> GetRecentTradesAsync(int count = 20)
    {
        try
        {
            var trades = await _httpClient.GetFromJsonAsync<List<TradeRecord>>($"{_baseUrl}/api/trades/recent?count={count}");
            return trades ?? new List<TradeRecord>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching trades: {ex.Message}");
            return new List<TradeRecord>();
        }
    }

    public async Task<DaemonStatus> GetHealthAsync()
    {
        try
        {
            var status = await _httpClient.GetFromJsonAsync<DaemonStatus>($"{_baseUrl}/api/health");
            return status ?? new DaemonStatus { DaemonRunning = false };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching health: {ex.Message}");
            return new DaemonStatus { DaemonRunning = false };
        }
    }
}
