namespace MobileUI.Api.Models;

public class TradeCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = ""; // SELL, SELL_ALL
    public string? Ticker { get; set; }
    public string Status { get; set; } = "pending"; // pending, queued_for_open, executing, filled, error, expired, cancelled
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(4);
    public string Source { get; set; } = "android-app";
    public double? FillPrice { get; set; }
    public int? Quantity { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SellRequest
{
    public string Ticker { get; set; } = "";
}

public class SellAllRequest
{
}

public class CommandResponse
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? ExecuteAtUtc { get; set; }
    public bool IsQueued { get; set; }
    public string Message { get; set; } = "";
}
