namespace MobileUI.Maui.Models;

public class Position
{
    public string Ticker { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double FillPrice { get; set; }
    public double CostValue { get; set; }
    public DateTime EntryDate { get; set; }
    public double StopLevel { get; set; }
    public double TargetLevel { get; set; }
    public double KellyFraction { get; set; }
    public double? CurrentPrice { get; set; }
    public double? UnrealizedPnl { get; set; }
}

public class TradeRecord
{
    public string Ticker { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public DateTime DateOpened { get; set; }
    public DateTime DateClosed { get; set; }
    public double EntryPrice { get; set; }
    public double ExitPrice { get; set; }
    public double Quantity { get; set; }
    public double RoundtripPnl { get; set; }
}

public class DaemonStatus
{
    public bool DaemonRunning { get; set; }
    public int? HeartbeatAgeSeconds { get; set; }
    public bool DryRun { get; set; }
    public bool HaltNewEntries { get; set; }
    public List<string> ReconciliationDiscrepancies { get; set; } = new();
    public string? LastReconcileDate { get; set; }
    public TradesDaily? TradesDaily { get; set; }
    public Dictionary<string, MarketStatus> Markets { get; set; } = new();
}

public class TradesDaily
{
    public string? Date { get; set; }
    public int Buys { get; set; }
    public int Sells { get; set; }
}

public class MarketStatus
{
    public bool InTradingHours { get; set; }
    public int LastCycleHour { get; set; }
}
