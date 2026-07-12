namespace MobileUI.Api.Models;

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
