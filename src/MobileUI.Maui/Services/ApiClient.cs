using System.Net.Http.Json;
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

    public ApiClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
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
