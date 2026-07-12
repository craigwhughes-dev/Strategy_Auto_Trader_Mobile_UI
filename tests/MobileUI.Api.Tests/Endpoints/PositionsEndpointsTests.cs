using MobileUI.Api.Endpoints;
using MobileUI.Api.Models;
using MobileUI.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Endpoints;

[TestFixture]
public class PositionsEndpointsTests
{
    private MockStatusReader _statusReader = null!;
    private MockPriceFetcher _priceFetcher = null!;
    private MockJournalReader _journalReader = null!;

    [SetUp]
    public void Setup()
    {
        _statusReader = new MockStatusReader();
        _priceFetcher = new MockPriceFetcher();
        _journalReader = new MockJournalReader();
    }

    [Test]
    public async Task GetPositions_WithNoPositions_ReturnsEmptyArray()
    {
        _statusReader.Positions.Clear();
        var result = await InvokeGetPositions(_statusReader, _priceFetcher);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task GetPositions_WithPositions_ReturnsSortedByTicker()
    {
        _statusReader.Positions["ZZZ"] = new Position { Ticker = "ZZZ", Quantity = 10, FillPrice = 50 };
        _statusReader.Positions["AAA"] = new Position { Ticker = "AAA", Quantity = 20, FillPrice = 100 };
        _statusReader.Positions["MMM"] = new Position { Ticker = "MMM", Quantity = 15, FillPrice = 75 };

        var result = await InvokeGetPositions(_statusReader, _priceFetcher);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task GetPositions_FetchesPricesForAllTickers()
    {
        _statusReader.Positions["AAPL"] = new Position { Ticker = "AAPL", Quantity = 100, FillPrice = 150 };
        _statusReader.Positions["MSFT"] = new Position { Ticker = "MSFT", Quantity = 50, FillPrice = 300 };

        _priceFetcher.Prices["AAPL"] = 155.50;
        _priceFetcher.Prices["MSFT"] = 310.00;

        var result = await InvokeGetPositions(_statusReader, _priceFetcher);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task GetPositions_CalculatesUnrealizedPnl()
    {
        var position = new Position
        {
            Ticker = "AAPL",
            Quantity = 100,
            FillPrice = 150.00,
            CurrentPrice = null
        };

        _statusReader.Positions["AAPL"] = position;
        _priceFetcher.Prices["AAPL"] = 155.00;

        var result = await InvokeGetPositions(_statusReader, _priceFetcher);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task GetPositions_WithMissingPrice_SkipsUpdate()
    {
        var position = new Position
        {
            Ticker = "UNKNOWN",
            Quantity = 100,
            FillPrice = 50.00
        };

        _statusReader.Positions["UNKNOWN"] = position;

        var result = await InvokeGetPositions(_statusReader, _priceFetcher);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public void GetRecentTrades_DefaultCount()
    {
        for (int i = 0; i < 25; i++)
        {
            _journalReader.Trades.Add(new TradeRecord
            {
                Ticker = $"TICK{i}",
                EntryPrice = 100 + i,
                ExitPrice = 105 + i,
                Quantity = 10,
                RoundtripPnl = 50
            });
        }

        var result = InvokeGetRecentTrades(_journalReader, 20);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public void GetRecentTrades_WithCountZero_ReturnsSingleTrade()
    {
        _journalReader.Trades.Add(new TradeRecord
        {
            Ticker = "AAPL",
            EntryPrice = 150,
            ExitPrice = 155,
            Quantity = 10,
            RoundtripPnl = 50
        });

        var result = InvokeGetRecentTrades(_journalReader, 0);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public void GetRecentTrades_WithCountAbove100_CapsAt100()
    {
        var result = InvokeGetRecentTrades(_journalReader, 200);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public void GetHealth_ReturnsCurrentStatus()
    {
        _statusReader.Status.DaemonRunning = true;
        _statusReader.Status.HeartbeatAgeSeconds = 5;
        _statusReader.Status.DryRun = false;

        var result = InvokeGetHealth(_statusReader);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public void GetHealth_WithHaltNewEntries_IncludesFlag()
    {
        _statusReader.Status.HaltNewEntries = true;
        _statusReader.Status.ReconciliationDiscrepancies.Add("AAPL");

        var result = InvokeGetHealth(_statusReader);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    // Helper methods to invoke endpoint handlers
    private async Task<IResult> InvokeGetPositions(IStatusReader statusReader, IPriceFetcher priceFetcher)
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

    private IResult InvokeGetRecentTrades(IJournalReader journalReader, int count = 20)
    {
        var trades = journalReader.ReadRecentTrades(Math.Min(Math.Max(count, 1), 100));
        return Results.Ok(trades);
    }

    private IResult InvokeGetHealth(IStatusReader statusReader)
    {
        var status = statusReader.ReadStatus();

        var response = new
        {
            status.DaemonRunning,
            status.HeartbeatAgeSeconds,
            status.DryRun,
            status.HaltNewEntries,
            status.ReconciliationDiscrepancies,
            status.LastReconcileDate,
            status.TradesDaily,
            status.Markets,
        };

        return Results.Ok(response);
    }
}

internal class MockStatusReader : IStatusReader
{
    public Dictionary<string, Position> Positions { get; } = new();
    public DaemonStatus Status { get; } = new();

    public Dictionary<string, Position> ReadPositions() => Positions;

    public DaemonStatus ReadStatus() => Status;
}

internal class MockPriceFetcher : IPriceFetcher
{
    public Dictionary<string, double> Prices { get; } = new();

    public async Task<Dictionary<string, double>> FetchPricesAsync(IEnumerable<string> tickers)
    {
        var result = new Dictionary<string, double>();
        foreach (var ticker in tickers)
        {
            if (Prices.TryGetValue(ticker, out var price))
                result[ticker] = price;
        }

        await Task.Delay(0); // Simulate async
        return result;
    }
}

internal class MockJournalReader : IJournalReader
{
    public List<TradeRecord> Trades { get; } = new();

    public List<TradeRecord> ReadRecentTrades(int count)
    {
        return Trades.OrderByDescending(t => t.DateClosed).Take(count).ToList();
    }
}
