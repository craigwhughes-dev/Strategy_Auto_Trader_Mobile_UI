using MobileUI.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Services;

[TestFixture]
public class StatusReaderTests
{
    private StatusReader _reader = null!;
    private IConfiguration _config = null!;
    private ILogger<StatusReader> _logger = null!;

    [SetUp]
    public void Setup()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DaemonState:AppStatusPath"] = Path.Combine(TestContext.CurrentContext.TestDirectory, "app_status.json"),
                ["DaemonState:JournalPath"] = Path.Combine(TestContext.CurrentContext.TestDirectory, "live.csv"),
            });

        _config = configBuilder.Build();
        _logger = new MockLogger<StatusReader>();
        _reader = new StatusReader(_config, _logger);
    }

    [TearDown]
    public void Cleanup()
    {
        var testFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "app_status.json");
        if (File.Exists(testFile))
            File.Delete(testFile);
    }

    [Test]
    public void ReadStatus_WithValidAppStatus_ParsesCorrectly()
    {
        var testFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "app_status.json");
        var recentHeartbeat = DateTime.UtcNow.ToString("O");
        var validJson = $@"{{
  ""schema_version"": 1,
  ""heartbeat_utc"": ""{recentHeartbeat}"",
  ""daemon_pid"": 17852,
  ""dry_run"": false,
  ""halt_new_entries"": false,
  ""reconciliation_discrepancies"": [],
  ""last_reconcile_date"": ""2026-07-06"",
  ""trades_today"": {{""date"": ""2026-07-07"", ""buys"": 0, ""sells"": 0}},
  ""markets"": {{""ftse"": {{""in_trading_hours"": false, ""last_cycle_hour"": -1}}}},
  ""positions"": {{}}
}}";

        File.WriteAllText(testFile, validJson);

        var status = _reader.ReadStatus();

        Assert.That(status.DaemonRunning, Is.True);
        Assert.That(status.DryRun, Is.False);
        Assert.That(status.HaltNewEntries, Is.False);
        Assert.That(status.Markets.ContainsKey("ftse"), Is.True);

        File.Delete(testFile);
    }

    [Test]
    public void ReadStatus_WithMissingFile_ReturnsDaemonNotRunning()
    {
        var status = _reader.ReadStatus();
        Assert.That(status.DaemonRunning, Is.False);
    }

    [Test]
    public void ReadStatus_WithHeartbeatExactlyNow_ShowsZeroAge()
    {
        var testFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "app_status.json");
        var nowUtc = DateTimeOffset.UtcNow.ToString("O");
        var json = $@"{{
  ""schema_version"": 1,
  ""heartbeat_utc"": ""{nowUtc}"",
  ""daemon_pid"": 123,
  ""dry_run"": false,
  ""halt_new_entries"": false,
  ""reconciliation_discrepancies"": [],
  ""last_reconcile_date"": ""2026-07-06"",
  ""trades_today"": {{""date"": ""2026-07-07"", ""buys"": 0, ""sells"": 0}},
  ""markets"": {{}},
  ""positions"": {{}}
}}";

        File.WriteAllText(testFile, json);

        var status = _reader.ReadStatus();

        Assert.That(status.HeartbeatAgeSeconds, Is.GreaterThanOrEqualTo(0), "Heartbeat age should be non-negative");
        Assert.That(status.HeartbeatAgeSeconds, Is.LessThanOrEqualTo(2), "Heartbeat age should be nearly zero");

        File.Delete(testFile);
    }

    [Test]
    public void ReadStatus_WithHeartbeatAgeAround60Seconds_IsAccurate()
    {
        var testFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "app_status.json");
        var sixtySecondsAgo = DateTimeOffset.UtcNow.AddSeconds(-60).ToString("O");
        var json = $@"{{
  ""schema_version"": 1,
  ""heartbeat_utc"": ""{sixtySecondsAgo}"",
  ""daemon_pid"": 123,
  ""dry_run"": false,
  ""halt_new_entries"": false,
  ""reconciliation_discrepancies"": [],
  ""last_reconcile_date"": ""2026-07-06"",
  ""trades_today"": {{""date"": ""2026-07-07"", ""buys"": 0, ""sells"": 0}},
  ""markets"": {{}},
  ""positions"": {{}}
}}";

        File.WriteAllText(testFile, json);

        var status = _reader.ReadStatus();

        Assert.That(status.HeartbeatAgeSeconds, Is.GreaterThanOrEqualTo(59), "Heartbeat age should be ~60s");
        Assert.That(status.HeartbeatAgeSeconds, Is.LessThanOrEqualTo(61), "Heartbeat age should not exceed 61s");
        Assert.That(status.DaemonRunning, Is.True, "Daemon should be running (60s < 180s threshold)");

        File.Delete(testFile);
    }

    [Test]
    public void ReadStatus_WithStaleHeartbeat_DetectsAsNotRunning()
    {
        var testFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "app_status.json");
        var fourMinutesAgo = DateTimeOffset.UtcNow.AddSeconds(-240).ToString("O");
        var json = $@"{{
  ""schema_version"": 1,
  ""heartbeat_utc"": ""{fourMinutesAgo}"",
  ""daemon_pid"": 123,
  ""dry_run"": false,
  ""halt_new_entries"": false,
  ""reconciliation_discrepancies"": [],
  ""last_reconcile_date"": ""2026-07-06"",
  ""trades_today"": {{""date"": ""2026-07-07"", ""buys"": 0, ""sells"": 0}},
  ""markets"": {{}},
  ""positions"": {{}}
}}";

        File.WriteAllText(testFile, json);

        var status = _reader.ReadStatus();

        Assert.That(status.HeartbeatAgeSeconds, Is.GreaterThan(180), "Heartbeat age should exceed stale threshold");
        Assert.That(status.DaemonRunning, Is.False, "Daemon should be marked as not running");

        File.Delete(testFile);
    }

    [Test]
    public void ReadStatus_WithIso8601PlusOffsetTimestamp_ParsesCorrectly()
    {
        var testFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "app_status.json");
        var nowWithOffset = DateTimeOffset.UtcNow.ToString("O");
        var json = $@"{{
  ""schema_version"": 1,
  ""heartbeat_utc"": ""{nowWithOffset}"",
  ""daemon_pid"": 123,
  ""dry_run"": false,
  ""halt_new_entries"": false,
  ""reconciliation_discrepancies"": [],
  ""last_reconcile_date"": ""2026-07-06"",
  ""trades_today"": {{""date"": ""2026-07-07"", ""buys"": 0, ""sells"": 0}},
  ""markets"": {{}},
  ""positions"": {{}}
}}";

        File.WriteAllText(testFile, json);

        var status = _reader.ReadStatus();

        Assert.That(status.HeartbeatAgeSeconds, Is.GreaterThanOrEqualTo(0), "Negative heartbeat age indicates timezone parsing error");
        Assert.That(status.DaemonRunning, Is.True, "Should parse ISO 8601 with offset correctly");

        File.Delete(testFile);
    }

    [Test]
    public void ReadPositions_WithValidPositions_ReturnsPositionsDictionary()
    {
        var testFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "app_status.json");
        var json = """
{
  "schema_version": 1,
  "heartbeat_utc": "2026-07-12T16:11:25Z",
  "daemon_pid": 123,
  "dry_run": false,
  "halt_new_entries": false,
  "reconciliation_discrepancies": [],
  "last_reconcile_date": "2026-07-06",
  "trades_today": {"date": "2026-07-07", "buys": 0, "sells": 0},
  "markets": {},
  "positions": {
    "AAPL": {
      "entry_date": "2026-01-01",
      "fill_price": 150.0,
      "quantity": 10.0,
      "cost_value": 1500.0,
      "market": "SP500",
      "currency": "USD",
      "stop_level": 140.0,
      "target_level": 160.0,
      "kelly_fraction": 0.05
    }
  }
}
""";

        File.WriteAllText(testFile, json);

        var positions = _reader.ReadPositions();

        Assert.That(positions.ContainsKey("AAPL"), Is.True);
        Assert.That(positions["AAPL"].Quantity, Is.EqualTo(10.0));
        Assert.That(positions["AAPL"].Currency, Is.EqualTo("USD"));

        File.Delete(testFile);
    }
}
