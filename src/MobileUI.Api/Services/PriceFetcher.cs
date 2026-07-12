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
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, (double Price, long CachedAtTicks)> _priceCache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(60);

    public PriceFetcher(HttpClient httpClient, ILogger<PriceFetcher> logger, TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Dictionary<string, double>> FetchPricesAsync(IEnumerable<string> tickers)
    {
        var prices = new Dictionary<string, double>();
        var tickersToFetch = new List<string>();
        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;

        foreach (var ticker in tickers)
        {
            var normalizedTicker = ticker.ToUpperInvariant();
            if (_priceCache.TryGetValue(normalizedTicker, out var cached))
            {
                if (nowTicks - cached.CachedAtTicks < _cacheDuration.Ticks)
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
                    _priceCache[ticker] = (price.Value, nowTicks);
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
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{yahooTicker}";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Yahoo Finance returned {StatusCode} for {Ticker}", response.StatusCode, ticker);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("chart", out var chart))
                return null;

            var results = chart.GetProperty("result");
            if (results.GetArrayLength() == 0)
                return null;

            var result = results[0];
            if (!result.TryGetProperty("meta", out var meta))
                return null;

            if (!meta.TryGetProperty("regularMarketPrice", out var priceElement))
                return null;

            var price = priceElement.GetDouble();

            if (meta.TryGetProperty("currency", out var currencyProp))
            {
                var currency = currencyProp.GetString() ?? "USD";
                _logger.LogDebug("Fetched price for {Ticker}: {Price} {Currency}", ticker, price, currency);
            }

            return price;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching price for ticker {Ticker}", ticker);
            return null;
        }
    }
}
