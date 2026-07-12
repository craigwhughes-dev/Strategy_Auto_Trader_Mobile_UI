using System.Text.Json;

namespace MobileUI.Api.Services;

public interface IPriceFetcher
{
    Task<Dictionary<string, double>> FetchPricesAsync(IEnumerable<string> tickers);
}

public class PriceFetcher : IPriceFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PriceFetcher> _logger;
    private readonly Dictionary<string, (double Price, DateTime CachedAt)> _priceCache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(60);

    public PriceFetcher(HttpClient httpClient, ILogger<PriceFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Dictionary<string, double>> FetchPricesAsync(IEnumerable<string> tickers)
    {
        var prices = new Dictionary<string, double>();
        var tickersToFetch = new List<string>();

        foreach (var ticker in tickers)
        {
            var normalizedTicker = ticker.ToUpperInvariant();
            if (_priceCache.TryGetValue(normalizedTicker, out var cached))
            {
                if (DateTime.UtcNow - cached.CachedAt < _cacheDuration)
                {
                    prices[normalizedTicker] = cached.Price;
                    continue;
                }
            }
            tickersToFetch.Add(normalizedTicker);
        }

        if (tickersToFetch.Count == 0)
            return prices;

        try
        {
            foreach (var ticker in tickersToFetch)
            {
                var price = await FetchSinglePriceAsync(ticker);
                if (price.HasValue)
                {
                    prices[ticker] = price.Value;
                    _priceCache[ticker] = (price.Value, DateTime.UtcNow);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching prices for tickers: {Tickers}", string.Join(",", tickersToFetch));
        }

        return prices;
    }

    private async Task<double?> FetchSinglePriceAsync(string ticker)
    {
        try
        {
            var yahooTicker = ticker.Contains('.') ? ticker : ticker;
            var url = $"https://query1.finance.yahoo.com/v10/finance/quoteSummary/{yahooTicker}?modules=price";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Yahoo Finance returned {StatusCode} for {Ticker}", response.StatusCode, ticker);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("quoteSummary", out var quoteSummary))
                return null;

            var results = quoteSummary.GetProperty("result");
            if (results.GetArrayLength() == 0)
                return null;

            var priceObj = results[0].GetProperty("price");
            var regularPrice = priceObj.GetProperty("regularMarketPrice").GetDouble();

            var currency = "USD";
            if (priceObj.TryGetProperty("currency", out var currencyProp))
            {
                currency = currencyProp.GetString() ?? "USD";
            }

            if (currency == "GBp")
            {
                regularPrice /= 100.0;
            }

            return regularPrice;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching price for ticker {Ticker}", ticker);
            return null;
        }
    }
}
