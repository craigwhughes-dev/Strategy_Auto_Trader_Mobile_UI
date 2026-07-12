using MobileUI.Api.Models;

namespace MobileUI.Api.Services;

public interface IJournalReader
{
    List<TradeRecord> ReadRecentTrades(int count = 20);
}

public class JournalReader : IJournalReader
{
    private readonly string _journalPath;
    private readonly ILogger<JournalReader> _logger;

    public JournalReader(IConfiguration configuration, ILogger<JournalReader> logger)
    {
        _journalPath = configuration["DaemonState:JournalPath"]
            ?? throw new ArgumentException("DaemonState:JournalPath not configured");
        _logger = logger;
    }

    public List<TradeRecord> ReadRecentTrades(int count = 20)
    {
        var trades = new List<TradeRecord>();

        try
        {
            if (!File.Exists(_journalPath))
            {
                _logger.LogWarning("Journal file not found at {Path}", _journalPath);
                return trades;
            }

            var lines = File.ReadAllLines(_journalPath);
            if (lines.Length < 2)
                return trades;

            var headers = lines[0].Split(',');
            var headerMap = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                headerMap[headers[i].Trim()] = i;
            }

            for (int i = lines.Length - 1; i >= 1 && trades.Count < count; i--)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 2)
                    continue;

                try
                {
                    var ticker = GetField(fields, headerMap, "ticker", "")?.ToUpperInvariant() ?? "";
                    if (string.IsNullOrEmpty(ticker))
                        continue;

                    var record = new TradeRecord
                    {
                        Ticker = ticker,
                        Market = GetField(fields, headerMap, "market", "") ?? "UNKNOWN",
                        Currency = GetField(fields, headerMap, "currency", "") ?? "USD",
                        DateOpened = TryParseDate(GetField(fields, headerMap, "date_opened", "")) ?? DateTime.MinValue,
                        DateClosed = TryParseDate(GetField(fields, headerMap, "date_closed", "")) ?? DateTime.MinValue,
                        EntryPrice = TryParseDouble(GetField(fields, headerMap, "entry_price", "")) ?? 0,
                        ExitPrice = TryParseDouble(GetField(fields, headerMap, "exit_price", "")) ?? 0,
                        Quantity = TryParseDouble(GetField(fields, headerMap, "quantity", "")) ?? 0,
                        RoundtripPnl = TryParseDouble(GetField(fields, headerMap, "pnl_usd", "")) ?? 0,
                    };

                    trades.Add(record);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing trade record at line {Line}", i);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading journal");
        }

        return trades;
    }

    private static string? GetField(string[] fields, Dictionary<string, int> headerMap, string fieldName, string defaultIfMissing)
    {
        if (headerMap.TryGetValue(fieldName, out var index) && index < fields.Length)
        {
            var value = fields[index].Trim();
            return string.IsNullOrEmpty(value) ? defaultIfMissing : value;
        }
        return defaultIfMissing;
    }

    private static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        return DateTime.TryParse(value, out var result) ? result : null;
    }

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        return double.TryParse(value, out var result) ? result : null;
    }
}
