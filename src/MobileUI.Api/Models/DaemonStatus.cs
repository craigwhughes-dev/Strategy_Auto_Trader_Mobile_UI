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
    public ShadowBenchmarkStatus? ShadowBenchmark { get; set; }
    public CrashRecoveryOverrideStatus? CrashRecoveryOverride { get; set; }
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
    public string? VvixBand { get; set; }
    public List<TierInfo> Tiers { get; set; } = new();
    public int? SelectedTierNum { get; set; }
    public string? SelectedAsset { get; set; }
    public string? Action { get; set; }
    public VvixStatus? Vvix { get; set; }
}

public class TierInfo
{
    public int TierNum { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Index { get; set; } = string.Empty;
    public double? GateValue { get; set; }
    public double? EnterGateValue { get; set; }
    public double? ExitGateValue { get; set; }
    public double? CurrentValue { get; set; }
    public bool Passes { get; set; }
}

public class VvixStatus
{
    public double? Current { get; set; }
    public string? Band { get; set; }
    public int CalmStreakDays { get; set; }
    public int StressedStreakDays { get; set; }
    public double? ConfirmDays { get; set; }
    public double? EdgeLow { get; set; }
    public double? EdgeHigh { get; set; }
    public string? LastDate { get; set; }
    public VxnThresholds? VxnThresholds { get; set; }
}

public class VxnThresholds
{
    public VxnPair? Calm { get; set; }
    public VxnPair? Base { get; set; }
    public VxnPair? Stressed { get; set; }
}

public class VxnPair
{
    public double Enter { get; set; }
    public double Exit { get; set; }
}

public class ShadowBenchmarkStatus
{
    public string Ticker { get; set; } = string.Empty;
    public string? Since { get; set; }
    public string? AsOf { get; set; }
    public double AmountGbp { get; set; }
    public double EntryPrice { get; set; }
    public double LatestPrice { get; set; }
    public double ValueGbp { get; set; }
    public double PnlGbp { get; set; }
    public double PnlPct { get; set; }
}

public class CrashRecoveryOverrideStatus
{
    public bool Enabled { get; set; }
    public bool EpisodeActive { get; set; }
    public string? EpisodePeakDate { get; set; }
    public bool ExitedToCash { get; set; }
    public double? HygRatio { get; set; }
    public int HygConfirmStreak { get; set; }
    public int HygExitStreak { get; set; }
    public bool HygStickyArmed { get; set; }
    public bool? NasdaqRising { get; set; }
    public bool Fired { get; set; }
    public bool Active { get; set; }
    public bool Resolved { get; set; }
    public string? AsOfDate { get; set; }
    public CrashRecoveryOverrideThresholds? Thresholds { get; set; }
}

public class CrashRecoveryOverrideThresholds
{
    public double HygRecoveryThreshold { get; set; }
    public int ConfirmDays { get; set; }
    public int ExitConfirmDays { get; set; }
    public int RiseDays { get; set; }
    public double MinDepth { get; set; }
}
