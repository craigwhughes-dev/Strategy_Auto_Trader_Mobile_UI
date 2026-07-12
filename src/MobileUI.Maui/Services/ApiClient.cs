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
    void SetCertificateThumbprint(string thumbprint);
    string GetBaseUrl();
}

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private string _baseUrl;
    private string _expectedCertificateThumbprint;
    private const string BaseUrlPrefsKey = "api_base_url";
    private const string ThumbprintPrefsKey = "api_cert_thumbprint";
    private const string DefaultBaseUrl = "http://192.168.1.100:5000";
    private const string DefaultThumbprint = "7618F28C90EE396840E9B980773F8A69147E86CC";

    public ApiClient()
    {
        _baseUrl = Preferences.Get(BaseUrlPrefsKey, DefaultBaseUrl);
        _expectedCertificateThumbprint = Preferences.Get(ThumbprintPrefsKey, DefaultThumbprint);

        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = ValidateServerCertificate;

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
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
        if (thumbprint != _expectedCertificateThumbprint)
            return false;

        if (errors == System.Net.Security.SslPolicyErrors.None)
            return true;

        return errors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;
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
}
