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
}
