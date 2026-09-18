namespace MobileUI.Api.Models;

public class DaemonStatus
{
    public bool DaemonRunning { get; set; }
    public int? HeartbeatAgeSeconds { get; set; }
    public bool DryRun { get; set; }
    public bool HaltNewEntries { get; set; }
    public bool PausedByUser { get; set; }
    public bool TierMode { get; set; }
    public List<string> ReconciliationDiscrepancies { get; set; } = new();
    public string? LastReconcileDate { get; set; }
    public TradesDaily? TradesDaily { get; set; }
    public Dictionary<string, MarketStatus> Markets { get; set; } = new();
    public TierAllocationStatus? TierAllocation { get; set; }
    public string? Error { get; set; }
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

public class TierAllocationStatus
{
    public double? VixCurrent { get; set; }
    public double? VxnCurrent { get; set; }
    public List<TierInfo> Tiers { get; set; } = new();
    public int? SelectedTierNum { get; set; }
    public string? SelectedAsset { get; set; }
    public string? Action { get; set; }
}

public class TierInfo
{
    public int TierNum { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Index { get; set; } = string.Empty;
    public double? GateValue { get; set; }
    public double? CurrentValue { get; set; }
    public bool Passes { get; set; }
}
