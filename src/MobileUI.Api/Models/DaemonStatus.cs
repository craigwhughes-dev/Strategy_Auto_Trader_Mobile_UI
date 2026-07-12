namespace MobileUI.Api.Models;

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
