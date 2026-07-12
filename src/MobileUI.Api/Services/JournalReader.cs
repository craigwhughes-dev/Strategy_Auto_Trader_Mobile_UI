using Microsoft.VisualBasic.FileIO;
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

            using var parser = new TextFieldParser(_journalPath)
            {
                TextFieldType = FieldType.Delimited,
                Delimiters = new[] { "," },
                TrimWhiteSpace = true
            };

            if (parser.EndOfData)
                return trades;

            var headers = parser.ReadFields();
            if (headers == null)
                return trades;

            var headerMap = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                headerMap[headers[i].Trim()] = i;
            }

            var allLines = new List<string[]>();
            while (!parser.EndOfData)
            {
                try
                {
                    var fields = parser.ReadFields();
                    if (fields != null && fields.Length >= 2)
                    {
                        allLines.Add(fields);
                    }
                }
                catch (MalformedLineException ex)
                {
                    _logger.LogWarning(ex, "Malformed CSV line at line {Line}", parser.LineNumber);
                }
            }

            var startIndex = Math.Max(0, allLines.Count - count);
            for (int i = allLines.Count - 1; i >= startIndex; i--)
            {
                try
                {
                    var ticker = GetField(allLines[i], headerMap, "ticker", "")?.ToUpperInvariant() ?? "";
                    if (string.IsNullOrEmpty(ticker))
                        continue;

                    var market = GetMarketFromTicker(ticker);
                    var currency = GetCurrencyFromTicker(ticker);

                    var record = new TradeRecord
                    {
                        Ticker = ticker,
                        Market = GetField(allLines[i], headerMap, "market", "") ?? market,
                        Currency = GetField(allLines[i], headerMap, "currency", "") ?? currency,
                        DateOpened = TryParseDate(GetField(allLines[i], headerMap, "date_opened", "")) ?? DateTime.MinValue,
                        DateClosed = TryParseDate(GetField(allLines[i], headerMap, "date_closed", "")) ?? DateTime.MinValue,
                        EntryPrice = TryParseDouble(GetField(allLines[i], headerMap, "entry_price", "")) ?? 0,
                        ExitPrice = TryParseDouble(GetField(allLines[i], headerMap, "exit_price", "")) ?? 0,
                        Quantity = TryParseDouble(GetField(allLines[i], headerMap, "quantity", "")) ?? 0,
                        RoundtripPnl = TryParseDouble(GetField(allLines[i], headerMap, "pnl_usd", "")) ?? 0,
                    };

                    trades.Add(record);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing trade record at index {Index}", i);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading journal");
        }

        return trades;
    }

    private static string GetMarketFromTicker(string ticker)
    {
        if (ticker.EndsWith(".L", StringComparison.OrdinalIgnoreCase))
            return "LSE";
        if (ticker.EndsWith(".US", StringComparison.OrdinalIgnoreCase))
            return "NASDAQ";
        return "UNKNOWN";
    }

    private static string GetCurrencyFromTicker(string ticker)
    {
        if (ticker.EndsWith(".L", StringComparison.OrdinalIgnoreCase))
            return "GBX";
        if (ticker.EndsWith(".US", StringComparison.OrdinalIgnoreCase))
            return "USD";
        return "USD";
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
