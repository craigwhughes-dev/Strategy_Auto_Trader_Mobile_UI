using MobileUI.Api.Models;
using MobileUI.Api.Services;

namespace MobileUI.Api.Endpoints;

public static class PositionsEndpoints
{
    public static void MapPositionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api")
            .WithName("Trading");

        group.MapGet("/positions", GetPositions)
            .WithName("GetPositions");

        group.MapGet("/trades/recent", GetRecentTrades)
            .WithName("GetRecentTrades");

        group.MapGet("/health", GetHealth)
            .WithName("GetHealth");
    }

    private static async Task<IResult> GetPositions(IStatusReader statusReader, IPriceFetcher priceFetcher)
    {
        var positions = statusReader.ReadPositions();

        if (!positions.Any())
            return Results.Ok(Array.Empty<Position>());

        var prices = await priceFetcher.FetchPricesAsync(positions.Keys);

        var positionList = new List<Position>();
        foreach (var (ticker, position) in positions)
        {
            if (prices.TryGetValue(ticker, out var price))
            {
                position.CurrentPrice = price;
                position.UnrealizedPnl = (price - position.FillPrice) * position.Quantity;
            }

            positionList.Add(position);
        }

        positionList.Sort((a, b) => a.Ticker.CompareTo(b.Ticker));
        return Results.Ok(positionList);
    }

    private static IResult GetRecentTrades(IJournalReader journalReader, int count = 20)
    {
        var trades = journalReader.ReadRecentTrades(Math.Min(Math.Max(count, 1), 100));
        return Results.Ok(trades);
    }

    private static IResult GetHealth(IStatusReader statusReader)
    {
        var status = statusReader.ReadStatus();
        return Results.Ok(status);
    }
}
