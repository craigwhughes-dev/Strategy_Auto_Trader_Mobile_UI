using StrategyTradingAppUI.Maui.Models;

namespace StrategyTradingAppUI.Maui.Services;

public interface IApiClient
{
    Task<List<Position>> GetPositionsAsync();
    Task<List<TradeRecord>> GetRecentTradesAsync(int count = 20);
    Task<DaemonStatus> GetHealthAsync();
    Task<CommandResponse> SellAsync(string ticker);
    Task<CommandResponse> SellAllAsync();
    Task<CommandResponse> PauseBuyingAsync();
    Task<CommandResponse> ResumeBuyingAsync();
    Task<List<TradeCommand>> GetCommandsAsync();
    Task<TradeCommand?> GetCommandAsync(string id);
    Task<string?> CancelCommandAsync(string id);
    void SetBaseUrl(string url);
    void SetCertificateThumbprint(string thumbprint);
    Task SetApiKeyAsync(string apiKey);
    Task<string> GetApiKeyAsync();
    string GetBaseUrl();
}
