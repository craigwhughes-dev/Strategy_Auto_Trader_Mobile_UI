using MobileUI.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Services;

[TestFixture]
public class JournalReaderTests
{
    private JournalReader _reader = null!;
    private IConfiguration _config = null!;
    private ILogger<JournalReader> _logger = null!;
    private string _journalPath = null!;

    [SetUp]
    public void Setup()
    {
        _journalPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "live.csv");
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DaemonState:JournalPath"] = _journalPath,
            });

        _config = configBuilder.Build();
        _logger = new MockLogger<JournalReader>();
        _reader = new JournalReader(_config, _logger);
    }

    [TearDown]
    public void Cleanup()
    {
        if (File.Exists(_journalPath))
            File.Delete(_journalPath);
    }

    [Test]
    public void ReadRecentTrades_WithValidCsv_ReturnsTrades()
    {
        var csv = """
ticker,market,currency,date_opened,date_closed,entry_price,exit_price,quantity,roundtrip_pnl
AAPL,SP500,USD,2026-01-01,2026-02-01,150.0,155.0,10.0,50.0
MSFT,SP500,USD,2026-01-05,2026-02-05,300.0,310.0,5.0,50.0
""";

        File.WriteAllText(_journalPath, csv);

        var trades = _reader.ReadRecentTrades(10);

        Assert.That(trades.Count, Is.EqualTo(2));
        Assert.That(trades[0].Ticker, Is.EqualTo("MSFT"));
        Assert.That(trades[1].Ticker, Is.EqualTo("AAPL"));
    }

    [Test]
    public void ReadRecentTrades_SkipsZeroPricePlaceholderRows()
    {
        var csv = """
ticker,market,currency,date_opened,date_closed,entry_price,exit_price,quantity,roundtrip_pnl
AAPL,SP500,USD,2026-07-05,2026-07-05,0.0,0.0,0.0,0.0
MSFT,SP500,USD,2026-01-05,2026-02-05,300.0,310.0,5.0,50.0
""";

        File.WriteAllText(_journalPath, csv);

        var trades = _reader.ReadRecentTrades(10);

        Assert.That(trades.Count, Is.EqualTo(1));
        Assert.That(trades[0].Ticker, Is.EqualTo("MSFT"));
    }

    [Test]
    public void ReadRecentTrades_SkipsZeroQuantityRow()
    {
        var csv = """
ticker,market,currency,date_opened,date_closed,entry_price,exit_price,quantity,roundtrip_pnl
AAPL,SP500,USD,2026-01-01,2026-02-01,150.0,155.0,0.0,0.0
""";

        File.WriteAllText(_journalPath, csv);

        var trades = _reader.ReadRecentTrades(10);

        Assert.That(trades.Count, Is.EqualTo(0));
    }

    [Test]
    public void ReadRecentTrades_WithMissingFile_ReturnsEmptyList()
    {
        var trades = _reader.ReadRecentTrades(10);
        Assert.That(trades.Count, Is.EqualTo(0));
    }

    [Test]
    public void ReadRecentTrades_RespectCount()
    {
        var csv = """
ticker,market,currency,date_opened,date_closed,entry_price,exit_price,quantity,roundtrip_pnl
AAPL,SP500,USD,2026-01-01,2026-02-01,150.0,155.0,10.0,50.0
MSFT,SP500,USD,2026-01-05,2026-02-05,300.0,310.0,5.0,50.0
GOOG,SP500,USD,2026-01-10,2026-02-10,2500.0,2550.0,1.0,50.0
""";

        File.WriteAllText(_journalPath, csv);

        var trades = _reader.ReadRecentTrades(2);

        Assert.That(trades.Count, Is.EqualTo(2));
    }

    [Test]
    public void ReadRecentTrades_WithoutMarketCurrencyColumns_DeriveFromTicker()
    {
        var csv = """
ticker,date_opened,date_closed,entry_price,exit_price,quantity,roundtrip_pnl
HSBA.L,2026-01-01,2026-02-01,500.0,550.0,10.0,500.0
AAPL,2026-01-05,2026-02-05,150.0,155.0,5.0,25.0
""";

        File.WriteAllText(_journalPath, csv);

        var trades = _reader.ReadRecentTrades(10);

        Assert.That(trades.Count, Is.EqualTo(2));

        var lseTrade = trades.FirstOrDefault(t => t.Ticker == "HSBA.L");
        Assert.That(lseTrade, Is.Not.Null);
        Assert.That(lseTrade!.Market, Is.EqualTo("LSE"));
        Assert.That(lseTrade.Currency, Is.EqualTo("GBX"));

        var nasdaqTrade = trades.FirstOrDefault(t => t.Ticker == "AAPL");
        Assert.That(nasdaqTrade, Is.Not.Null);
        Assert.That(nasdaqTrade!.Market, Is.EqualTo("UNKNOWN"));
        Assert.That(nasdaqTrade.Currency, Is.EqualTo("USD"));
    }

    [Test]
    public void ReadRecentTrades_WithPopulatedMarketCurrencyColumns_UsesExplicitValues()
    {
        var csv = """
ticker,market,currency,date_opened,date_closed,entry_price,exit_price,quantity,roundtrip_pnl
HSBA.L,LSE,GBX,2026-01-01,2026-02-01,500.0,550.0,10.0,500.0
AAPL,NASDAQ,USD,2026-01-05,2026-02-05,150.0,155.0,5.0,25.0
""";

        File.WriteAllText(_journalPath, csv);

        var trades = _reader.ReadRecentTrades(10);

        Assert.That(trades.Count, Is.EqualTo(2));

        var lseTrade = trades.FirstOrDefault(t => t.Ticker == "HSBA.L");
        Assert.That(lseTrade, Is.Not.Null);
        Assert.That(lseTrade!.Market, Is.EqualTo("LSE"));
        Assert.That(lseTrade.Currency, Is.EqualTo("GBX"));

        var nasdaqTrade = trades.FirstOrDefault(t => t.Ticker == "AAPL");
        Assert.That(nasdaqTrade, Is.Not.Null);
        Assert.That(nasdaqTrade!.Market, Is.EqualTo("NASDAQ"));
        Assert.That(nasdaqTrade.Currency, Is.EqualTo("USD"));
    }

    [Test]
    public void ReadRecentTrades_WithEmptyMarketCurrencyValues_FallsBackToTickerDerived()
    {
        var csv = """
ticker,market,currency,date_opened,date_closed,entry_price,exit_price,quantity,roundtrip_pnl
HSBA.L, , ,2026-01-01,2026-02-01,500.0,550.0,10.0,500.0
AAPL, , ,2026-01-05,2026-02-05,150.0,155.0,5.0,25.0
""";

        File.WriteAllText(_journalPath, csv);

        var trades = _reader.ReadRecentTrades(10);

        Assert.That(trades.Count, Is.EqualTo(2));

        var lseTrade = trades.FirstOrDefault(t => t.Ticker == "HSBA.L");
        Assert.That(lseTrade, Is.Not.Null);
        Assert.That(lseTrade!.Market, Is.EqualTo("LSE"));
        Assert.That(lseTrade.Currency, Is.EqualTo("GBX"));

        var aapl = trades.FirstOrDefault(t => t.Ticker == "AAPL");
        Assert.That(aapl, Is.Not.Null);
        Assert.That(aapl!.Market, Is.EqualTo("UNKNOWN"));
        Assert.That(aapl.Currency, Is.EqualTo("USD"));
    }
}
