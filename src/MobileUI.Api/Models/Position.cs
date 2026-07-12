namespace MobileUI.Api.Models;

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
