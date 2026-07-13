using MobileUI.Api.Models;
using MobileUI.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Services;

[TestFixture]
public class CommandManagerTests
{
    private CommandManager _manager = null!;
    private IConfiguration _config = null!;
    private ILogger<CommandManager> _logger = null!;
    private IStatusReader _statusReader = null!;
    private string _commandsPath = null!;

    [SetUp]
    public void Setup()
    {
        _commandsPath = Path.Combine(Path.GetTempPath(), $"test-commands-{Guid.NewGuid()}");
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DaemonState:CommandsPath"] = _commandsPath,
            });

        _config = configBuilder.Build();
        _logger = new MockLogger<CommandManager>();
        _statusReader = new MockStatusReader();
        _manager = new CommandManager(_config, _statusReader, _logger);
    }

    [TearDown]
    public void Cleanup()
    {
        if (Directory.Exists(_commandsPath))
            Directory.Delete(_commandsPath, recursive: true);
    }

    [Test]
    public async Task CreateSellCommandAsync_WithValidTicker_CreatesCommand()
    {
        var commandId = await _manager.CreateSellCommandAsync("AAPL");

        Assert.That(commandId, Is.Not.Null.And.Not.Empty);
        var command = await _manager.GetCommandAsync(commandId);
        Assert.That(command, Is.Not.Null);
        Assert.That(command!.Action, Is.EqualTo("SELL"));
        Assert.That(command.Ticker, Is.EqualTo("AAPL"));
    }

    [Test]
    public void CreateSellCommandAsync_WithNonExistentTicker_ThrowsException()
    {
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.CreateSellCommandAsync("NONEXISTENT"));
    }

    [Test]
    public async Task CreateSellCommandAsync_NormalizesTickerToUppercase()
    {
        var commandId = await _manager.CreateSellCommandAsync("aapl");
        var command = await _manager.GetCommandAsync(commandId);

        Assert.That(command!.Ticker, Is.EqualTo("AAPL"));
    }

    [Test]
    public async Task CreateSellAllCommandAsync_CreatesCommand()
    {
        var commandId = await _manager.CreateSellAllCommandAsync();

        Assert.That(commandId, Is.Not.Null.And.Not.Empty);
        var command = await _manager.GetCommandAsync(commandId);
        Assert.That(command, Is.Not.Null);
        Assert.That(command!.Action, Is.EqualTo("SELL_ALL"));
        Assert.That(command.Ticker, Is.Null.Or.Empty);
    }

    [Test]
    public async Task CreateSellCommandAsync_SetsExpiryToFourHours()
    {
        var beforeCreation = DateTime.UtcNow;
        var commandId = await _manager.CreateSellCommandAsync("AAPL");
        var afterCreation = DateTime.UtcNow;

        var command = await _manager.GetCommandAsync(commandId);
        Assert.That(command!.ExpiresAtUtc, Is.GreaterThanOrEqualTo(beforeCreation.AddHours(4)));
        Assert.That(command.ExpiresAtUtc, Is.LessThanOrEqualTo(afterCreation.AddHours(4)));
    }

    [Test]
    public async Task GetCommandAsync_WithInvalidId_ReturnsNull()
    {
        var command = await _manager.GetCommandAsync("nonexistent-id");
        Assert.That(command, Is.Null);
    }

    [Test]
    public async Task GetPendingCommandsAsync_ReturnsPendingCommands()
    {
        var id1 = await _manager.CreateSellAllCommandAsync();
        var id2 = await _manager.CreateSellCommandAsync("MSFT");

        var pending = await _manager.GetPendingCommandsAsync();

        Assert.That(pending.Count, Is.EqualTo(2));
        Assert.That(pending.Any(c => c.Id == id1), Is.True);
        Assert.That(pending.Any(c => c.Id == id2), Is.True);
    }

    [Test]
    public async Task GetPendingCommandsAsync_ReturnsCommandsInReverseChronological()
    {
        var id1 = await _manager.CreateSellAllCommandAsync();
        await Task.Delay(10); // Ensure different RequestedAtUtc
        var id2 = await _manager.CreateSellCommandAsync("MSFT");

        var pending = await _manager.GetPendingCommandsAsync();

        Assert.That(pending[0].Id, Is.EqualTo(id2)); // Newer first
        Assert.That(pending[1].Id, Is.EqualTo(id1)); // Older second
    }

    [Test]
    public async Task GetPendingCommandsAsync_WithNoPending_ReturnsEmptyList()
    {
        var pending = await _manager.GetPendingCommandsAsync();
        Assert.That(pending.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task CancelCommandAsync_WithPendingCommand_Succeeds()
    {
        var commandId = await _manager.CreateSellAllCommandAsync();

        var result = await _manager.CancelCommandAsync(commandId);

        Assert.That(result, Is.True);
        var command = await _manager.GetCommandAsync(commandId);
        Assert.That(command, Is.Not.Null);
        Assert.That(command!.Status, Is.EqualTo("cancelled"));
    }

    [Test]
    public async Task CancelCommandAsync_WithNonExistentCommand_ReturnsFalse()
    {
        var result = await _manager.CancelCommandAsync("nonexistent-id");
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task CancelCommandAsync_WithQueuedForOpenCommand_Succeeds()
    {
        var commandId = await _manager.CreateSellAllCommandAsync();
        var pendingFile = Path.Combine(_commandsPath, "pending", $"{commandId}.json");

        // Manually update status to queued_for_open
        var json = await File.ReadAllTextAsync(pendingFile);
        var cmd = System.Text.Json.JsonSerializer.Deserialize<MobileUI.Api.Models.TradeCommand>(json);
        cmd!.Status = "queued_for_open";
        var updatedJson = System.Text.Json.JsonSerializer.Serialize(cmd, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(pendingFile, updatedJson);

        var result = await _manager.CancelCommandAsync(commandId);

        Assert.That(result, Is.True);
        var doneFile = Path.Combine(_commandsPath, "done", $"{commandId}.cancelled.json");
        Assert.That(File.Exists(doneFile), Is.True);
    }

    [Test]
    public async Task CancelCommandAsync_WritesResultsFileWithCancelledStatus()
    {
        var commandId = await _manager.CreateSellAllCommandAsync();

        var result = await _manager.CancelCommandAsync(commandId);

        Assert.That(result, Is.True);
        var resultsFile = Path.Combine(_commandsPath, "results", $"{commandId}.json");
        Assert.That(File.Exists(resultsFile), Is.True);

        var resultJson = await File.ReadAllTextAsync(resultsFile);
        var resultCmd = System.Text.Json.JsonSerializer.Deserialize<MobileUI.Api.Models.TradeCommand>(resultJson);
        Assert.That(resultCmd!.Status, Is.EqualTo("cancelled"));
    }

    [Test]
    public async Task CreateSellCommandAsync_RejectsWhenQueuedForOpenExists()
    {
        var commandId = await _manager.CreateSellCommandAsync("AAPL");
        var pendingFile = Path.Combine(_commandsPath, "pending", $"{commandId}.json");

        // Manually update status to queued_for_open
        var json = await File.ReadAllTextAsync(pendingFile);
        var cmd = System.Text.Json.JsonSerializer.Deserialize<MobileUI.Api.Models.TradeCommand>(json);
        cmd!.Status = "queued_for_open";
        var updatedJson = System.Text.Json.JsonSerializer.Serialize(cmd, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(pendingFile, updatedJson);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.CreateSellCommandAsync("AAPL"));
    }

    [Test]
    public async Task WriteCommandAsync_UsesAtomicRename()
    {
        var commandId = await _manager.CreateSellAllCommandAsync();
        var pendingFile = Path.Combine(_commandsPath, "pending", $"{commandId}.json");

        Assert.That(File.Exists(pendingFile), Is.True);
        Assert.That(!File.Exists(pendingFile + ".tmp"), "Temp file should not exist after write");
    }

    [Test]
    public async Task CreatePauseBuyingCommandAsync_CreatesCommand()
    {
        var commandId = await _manager.CreatePauseBuyingCommandAsync();

        Assert.That(commandId, Is.Not.Null.And.Not.Empty);
        var command = await _manager.GetCommandAsync(commandId);
        Assert.That(command, Is.Not.Null);
        Assert.That(command!.Action, Is.EqualTo("PAUSE_BUYING"));
        Assert.That(command.Ticker, Is.Null.Or.Empty);
    }

    [Test]
    public async Task CreateResumeBuyingCommandAsync_CreatesCommand()
    {
        var commandId = await _manager.CreateResumeBuyingCommandAsync();

        Assert.That(commandId, Is.Not.Null.And.Not.Empty);
        var command = await _manager.GetCommandAsync(commandId);
        Assert.That(command, Is.Not.Null);
        Assert.That(command!.Action, Is.EqualTo("RESUME_BUYING"));
        Assert.That(command.Ticker, Is.Null.Or.Empty);
    }

    [Test]
    public async Task CreatePauseBuyingCommandAsync_RejectsWhenPendingExists()
    {
        await _manager.CreatePauseBuyingCommandAsync();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.CreatePauseBuyingCommandAsync());
    }

    [Test]
    public async Task CreateResumeBuyingCommandAsync_RejectsWhenPendingExists()
    {
        await _manager.CreateResumeBuyingCommandAsync();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.CreateResumeBuyingCommandAsync());
    }

    [Test]
    public async Task CreatePauseBuyingCommandAsync_AllowsAfterCancel()
    {
        var id1 = await _manager.CreatePauseBuyingCommandAsync();
        await _manager.CancelCommandAsync(id1);

        // Should succeed since previous command was cancelled
        var id2 = await _manager.CreatePauseBuyingCommandAsync();
        Assert.That(id2, Is.Not.EqualTo(id1));
    }
}

internal class MockStatusReader : IStatusReader
{
    private readonly Dictionary<string, Position> _positions = new()
    {
        ["AAPL"] = new Position { Ticker = "AAPL", Quantity = 100, FillPrice = 150 },
        ["MSFT"] = new Position { Ticker = "MSFT", Quantity = 50, FillPrice = 300 }
    };

    public Dictionary<string, Position> ReadPositions() => _positions;

    public DaemonStatus ReadStatus()
    {
        throw new NotImplementedException();
    }
}
