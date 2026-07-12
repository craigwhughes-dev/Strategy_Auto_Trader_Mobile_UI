using System.Text.Json;
using MobileUI.Api.Models;

namespace MobileUI.Api.Services;

public interface IStatusReader
{
    DaemonStatus ReadStatus();
    Dictionary<string, Position> ReadPositions();
}

public class StatusReader : IStatusReader
{
    private readonly string _appStatusPath;
    private readonly ILogger<StatusReader> _logger;
    private const int SchemaVersion = 1;
    private const int StaleHeartbeatThresholdSeconds = 180;

    public StatusReader(IConfiguration configuration, ILogger<StatusReader> logger)
    {
        _appStatusPath = configuration["DaemonState:AppStatusPath"]
            ?? throw new ArgumentException("DaemonState:AppStatusPath not configured");
        _logger = logger;
    }

    public DaemonStatus ReadStatus()
    {
        try
        {
            if (!File.Exists(_appStatusPath))
            {
                _logger.LogWarning("app_status.json not found at {Path}", _appStatusPath);
                return new DaemonStatus
                {
                    DaemonRunning = false,
                    Error = "Status file not found"
                };
            }

            var json = File.ReadAllText(_appStatusPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var schemaVersion = root.GetProperty("schema_version").GetInt32();
            if (schemaVersion != SchemaVersion)
            {
                _logger.LogError("Schema version mismatch: got {Got}, expected {Expected}",
                    schemaVersion, SchemaVersion);
                return new DaemonStatus
                {
                    DaemonRunning = false,
                    Error = $"Schema version {schemaVersion} not supported"
                };
            }

            var heartbeatUtc = root.GetProperty("heartbeat_utc").GetString();
            var heartbeatOffset = DateTimeOffset.Parse(heartbeatUtc!);
            var heartbeatAgeTotalSeconds = (DateTimeOffset.UtcNow - heartbeatOffset).TotalSeconds;
            var heartbeatAge = (int?)Math.Max(0, heartbeatAgeTotalSeconds);

            var status = new DaemonStatus
            {
                DaemonRunning = heartbeatAge < StaleHeartbeatThresholdSeconds,
                HeartbeatAgeSeconds = heartbeatAge,
                DryRun = root.GetProperty("dry_run").GetBoolean(),
                HaltNewEntries = root.GetProperty("halt_new_entries").GetBoolean(),
                LastReconcileDate = root.GetProperty("last_reconcile_date").GetString(),
            };

            _logger.LogInformation("✓ Daemon status read: Running={DaemonRunning}, HeartbeatAge={Age}s, DryRun={DryRun}, HaltNewEntries={Halt}",
                status.DaemonRunning, status.HeartbeatAgeSeconds, status.DryRun, status.HaltNewEntries);

            if (root.TryGetProperty("reconciliation_discrepancies", out var discrepancies))
            {
                status.ReconciliationDiscrepancies = discrepancies.EnumerateArray()
                    .Select(d => d.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            if (root.TryGetProperty("trades_today", out var tradesDaily))
            {
                status.TradesDaily = new TradesDaily
                {
                    Date = tradesDaily.TryGetProperty("date", out var date)
                        ? date.GetString()
                        : null,
                    Buys = tradesDaily.TryGetProperty("buys", out var buys)
                        ? buys.GetInt32()
                        : 0,
                    Sells = tradesDaily.TryGetProperty("sells", out var sells)
                        ? sells.GetInt32()
                        : 0,
                };
            }

            if (root.TryGetProperty("markets", out var markets))
            {
                foreach (var market in markets.EnumerateObject())
                {
                    var marketObj = market.Value;
                    status.Markets[market.Name] = new MarketStatus
                    {
                        InTradingHours = marketObj.TryGetProperty("in_trading_hours", out var inHours)
                            ? inHours.GetBoolean()
                            : false,
                        LastCycleHour = marketObj.TryGetProperty("last_cycle_hour", out var hour)
                            ? hour.GetInt32()
                            : -1,
                    };
                }
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading daemon status");
            return new DaemonStatus
            {
                DaemonRunning = false,
                Error = $"Failed to read status: {ex.GetType().Name}"
            };
        }
    }

    public Dictionary<string, Position> ReadPositions()
    {
        var positions = new Dictionary<string, Position>();

        try
        {
            if (!File.Exists(_appStatusPath))
                return positions;

            var json = File.ReadAllText(_appStatusPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("positions", out var positionsElement))
                return positions;

            foreach (var posObj in positionsElement.EnumerateObject())
            {
                var ticker = posObj.Name.ToUpperInvariant();
                var pos = posObj.Value;

                var position = new Position
                {
                    Ticker = ticker,
                    Market = pos.TryGetProperty("market", out var market)
                        ? market.GetString() ?? "UNKNOWN"
                        : "UNKNOWN",
                    Currency = pos.TryGetProperty("currency", out var currency)
                        ? currency.GetString() ?? "USD"
                        : "USD",
                    Quantity = pos.TryGetProperty("quantity", out var qty)
                        ? qty.GetDouble()
                        : 0,
                    FillPrice = pos.TryGetProperty("fill_price", out var fill)
                        ? fill.GetDouble()
                        : 0,
                    CostValue = pos.TryGetProperty("cost_value", out var cost)
                        ? cost.GetDouble()
                        : 0,
                    EntryDate = pos.TryGetProperty("entry_date", out var entry) && DateTime.TryParse(entry.GetString(), out var entryDate)
                        ? entryDate
                        : DateTime.MinValue,
                    StopLevel = pos.TryGetProperty("stop_level", out var stop)
                        ? stop.GetDouble()
                        : 0,
                    TargetLevel = pos.TryGetProperty("target_level", out var target)
                        ? target.GetDouble()
                        : 0,
                    KellyFraction = pos.TryGetProperty("kelly_fraction", out var kelly)
                        ? kelly.GetDouble()
                        : 0,
                };

                positions[ticker] = position;
            }

            if (positions.Count == 0)
            {
                _logger.LogInformation("✓ No open positions found (market may be closed or all positions closed)");
            }
            else
            {
                _logger.LogInformation("✓ Loaded {PositionCount} open position(s): {Tickers}",
                    positions.Count, string.Join(", ", positions.Keys));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading positions");
        }

        return positions;
    }
}
