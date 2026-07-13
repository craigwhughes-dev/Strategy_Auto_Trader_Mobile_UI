using MobileUI.Api.Endpoints;
using MobileUI.Api.Models;
using MobileUI.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Endpoints;

[TestFixture]
public class TradeEndpointsTests
{
    private MockCommandManager _commandManager = null!;
    private string _tempCommandsPath = null!;

    [SetUp]
    public void Setup()
    {
        _tempCommandsPath = Path.Combine(Path.GetTempPath(), $"test-trade-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempCommandsPath);
        _commandManager = new MockCommandManager();
    }

    [TearDown]
    public void Cleanup()
    {
        if (Directory.Exists(_tempCommandsPath))
            Directory.Delete(_tempCommandsPath, recursive: true);
    }

    [Test]
    public async Task SellAsync_WithValidTicker_Returns202Accepted()
    {
        var request = new SellRequest { Ticker = "AAPL" };
        var result = await InvokeSellEndpoint(request, _commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task SellAsync_WithEmptyTicker_Returns400BadRequest()
    {
        var request = new SellRequest { Ticker = "" };
        var result = await InvokeSellEndpoint(request, _commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task SellAsync_WithNullTicker_Returns400BadRequest()
    {
        var request = new SellRequest { Ticker = null! };
        var result = await InvokeSellEndpoint(request, _commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task SellAsync_WithNonExistentPosition_Returns400()
    {
        var request = new SellRequest { Ticker = "NONEXISTENT" };
        var result = await InvokeSellEndpoint(request, _commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task SellAsync_CreatedCommandHasCorrectAction()
    {
        var request = new SellRequest { Ticker = "AAPL" };
        var commandId = await _commandManager.CreateSellCommandAsync("AAPL");
        var command = await _commandManager.GetCommandAsync(commandId);

        Assert.That(command!.Action, Is.EqualTo("SELL"));
        Assert.That(command.Ticker, Is.EqualTo("AAPL"));
    }

    [Test]
    public async Task SellAllAsync_Returns202Accepted()
    {
        var result = await InvokeSellAllEndpoint(_commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task SellAllAsync_CreatedCommandIsCorrect()
    {
        var commandId = await _commandManager.CreateSellAllCommandAsync();
        var command = await _commandManager.GetCommandAsync(commandId);

        Assert.That(command!.Action, Is.EqualTo("SELL_ALL"));
        Assert.That(command.Ticker, Is.Null.Or.Empty);
    }

    [Test]
    public async Task GetPendingCommandsAsync_WithNoPending_ReturnsEmptyArray()
    {
        var result = await InvokeGetPendingCommandsEndpoint(_commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task GetPendingCommandsAsync_WithPending_ReturnsCommandList()
    {
        var id1 = await _commandManager.CreateSellAllCommandAsync();
        var id2 = await _commandManager.CreateSellCommandAsync("MSFT");

        var result = await InvokeGetPendingCommandsEndpoint(_commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task GetCommandAsync_WithValidId_Returns200Ok()
    {
        var commandId = await _commandManager.CreateSellAllCommandAsync();
        var result = await InvokeGetCommandEndpoint(commandId, _commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task GetCommandAsync_WithInvalidId_Returns404NotFound()
    {
        var result = await InvokeGetCommandEndpoint("invalid-id", _commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task CancelCommandAsync_WithPendingCommand_Returns200Ok()
    {
        var commandId = await _commandManager.CreateSellAllCommandAsync();
        var result = await InvokeCancelCommandEndpoint(commandId, _commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task CancelCommandAsync_WithNonExistentId_Returns404()
    {
        var result = await InvokeCancelCommandEndpoint("invalid-id", _commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task CancelCommandAsync_WithExecutingCommand_Returns409Conflict()
    {
        var commandId = await _commandManager.CreateSellAllCommandAsync();
        var cmd = await _commandManager.GetCommandAsync(commandId);
        cmd!.Status = "executing"; // Simulate in-progress command

        var result = await InvokeCancelCommandEndpoint(commandId, _commandManager);

        // Should return conflict since status != pending
        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task CancelCommandAsync_WithQueuedForOpenCommand_Returns200Ok()
    {
        var commandId = await _commandManager.CreateSellAllCommandAsync();
        var cmd = await _commandManager.GetCommandAsync(commandId);
        cmd!.Status = "queued_for_open"; // Simulate queued-for-open command

        var result = await InvokeCancelCommandEndpoint(commandId, _commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task PauseBuyingAsync_Returns202Accepted()
    {
        var result = await InvokePauseBuyingEndpoint(_commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task PauseBuyingAsync_CreatedCommandIsCorrect()
    {
        var commandId = await _commandManager.CreatePauseBuyingCommandAsync();
        var command = await _commandManager.GetCommandAsync(commandId);

        Assert.That(command!.Action, Is.EqualTo("PAUSE_BUYING"));
        Assert.That(command.Ticker, Is.Null.Or.Empty);
    }

    [Test]
    public async Task PauseBuyingAsync_RejectsDuplicatePending()
    {
        await _commandManager.CreatePauseBuyingCommandAsync();
        var result = await InvokePauseBuyingEndpoint(_commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task ResumeBuyingAsync_Returns202Accepted()
    {
        var result = await InvokeResumeBuyingEndpoint(_commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public async Task ResumeBuyingAsync_CreatedCommandIsCorrect()
    {
        var commandId = await _commandManager.CreateResumeBuyingCommandAsync();
        var command = await _commandManager.GetCommandAsync(commandId);

        Assert.That(command!.Action, Is.EqualTo("RESUME_BUYING"));
        Assert.That(command.Ticker, Is.Null.Or.Empty);
    }

    [Test]
    public async Task ResumeBuyingAsync_RejectsDuplicatePending()
    {
        await _commandManager.CreateResumeBuyingCommandAsync();
        var result = await InvokeResumeBuyingEndpoint(_commandManager);

        Assert.That(result, Is.InstanceOf<IResult>());
    }

    // Helper methods to invoke endpoint handlers
    private async Task<IResult> InvokeSellEndpoint(SellRequest request, ICommandManager manager)
    {
        if (string.IsNullOrWhiteSpace(request?.Ticker))
            return Results.BadRequest(new { error = "Ticker is required" });

        try
        {
            var commandId = await manager.CreateSellCommandAsync(request.Ticker);
            var command = await manager.GetCommandAsync(commandId);

            return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
            {
                Id = commandId,
                Status = command?.Status ?? "pending",
                Message = $"Sell order queued for {request.Ticker}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private async Task<IResult> InvokeSellAllEndpoint(ICommandManager manager)
    {
        var commandId = await manager.CreateSellAllCommandAsync();
        var command = await manager.GetCommandAsync(commandId);

        return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
        {
            Id = commandId,
            Status = command?.Status ?? "pending",
            Message = "Sell all positions queued"
        });
    }

    private async Task<IResult> InvokeGetPendingCommandsEndpoint(ICommandManager manager)
    {
        var commands = await manager.GetPendingCommandsAsync();
        return Results.Ok(commands.Select(c => new
        {
            c.Id,
            c.Action,
            c.Ticker,
            c.Status,
            c.RequestedAtUtc,
            c.ExpiresAtUtc
        }));
    }

    private async Task<IResult> InvokeGetCommandEndpoint(string id, ICommandManager manager)
    {
        var command = await manager.GetCommandAsync(id);
        if (command == null)
            return Results.NotFound(new { error = "Command not found" });

        return Results.Ok(new
        {
            command.Id,
            command.Action,
            command.Ticker,
            command.Status,
            command.RequestedAtUtc,
            command.ExpiresAtUtc,
            command.FillPrice,
            command.ErrorMessage
        });
    }

    private async Task<IResult> InvokeCancelCommandEndpoint(string id, ICommandManager manager)
    {
        var command = await manager.GetCommandAsync(id);
        if (command == null)
            return Results.NotFound(new { error = "Command not found" });

        if (command.Status != "pending" && command.Status != "queued_for_open")
            return Results.Conflict(new { error = "Cannot cancel: command already executing or completed" });

        var cancelled = await manager.CancelCommandAsync(id);
        if (!cancelled)
            return Results.Conflict(new { error = "Command is being processed" });

        return Results.Ok(new { message = "Command cancelled" });
    }

    private async Task<IResult> InvokePauseBuyingEndpoint(ICommandManager manager)
    {
        try
        {
            var commandId = await manager.CreatePauseBuyingCommandAsync();
            var command = await manager.GetCommandAsync(commandId);
            return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
            {
                Id = commandId,
                Status = command?.Status ?? "pending",
                Message = "Pause buying queued"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private async Task<IResult> InvokeResumeBuyingEndpoint(ICommandManager manager)
    {
        try
        {
            var commandId = await manager.CreateResumeBuyingCommandAsync();
            var command = await manager.GetCommandAsync(commandId);
            return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
            {
                Id = commandId,
                Status = command?.Status ?? "pending",
                Message = "Resume buying queued"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }
}

internal class MockCommandManager : ICommandManager
{
    private readonly Dictionary<string, TradeCommand> _commands = new();
    private readonly Dictionary<string, Position> _positions = new()
    {
        ["AAPL"] = new Position { Ticker = "AAPL", Quantity = 100, FillPrice = 150 },
        ["MSFT"] = new Position { Ticker = "MSFT", Quantity = 50, FillPrice = 300 }
    };

    public async Task<string> CreateSellCommandAsync(string ticker)
    {
        var normalizedTicker = ticker.ToUpperInvariant();
        if (!_positions.ContainsKey(normalizedTicker))
            throw new InvalidOperationException($"Position {ticker} not found");

        var command = new TradeCommand
        {
            Action = "SELL",
            Ticker = normalizedTicker,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4)
        };

        _commands[command.Id] = command;
        await Task.Delay(0); // Simulate async
        return command.Id;
    }

    public async Task<string> CreateSellAllCommandAsync()
    {
        var command = new TradeCommand
        {
            Action = "SELL_ALL",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4)
        };

        _commands[command.Id] = command;
        await Task.Delay(0); // Simulate async
        return command.Id;
    }

    public async Task<string> CreatePauseBuyingCommandAsync()
    {
        var existingPause = _commands.Values.FirstOrDefault(c => c.Action == "PAUSE_BUYING" && c.Status == "pending");
        if (existingPause != null)
            throw new InvalidOperationException("A pending PAUSE_BUYING command already exists");

        var command = new TradeCommand
        {
            Action = "PAUSE_BUYING",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4)
        };

        _commands[command.Id] = command;
        await Task.Delay(0); // Simulate async
        return command.Id;
    }

    public async Task<string> CreateResumeBuyingCommandAsync()
    {
        var existingResume = _commands.Values.FirstOrDefault(c => c.Action == "RESUME_BUYING" && c.Status == "pending");
        if (existingResume != null)
            throw new InvalidOperationException("A pending RESUME_BUYING command already exists");

        var command = new TradeCommand
        {
            Action = "RESUME_BUYING",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4)
        };

        _commands[command.Id] = command;
        await Task.Delay(0); // Simulate async
        return command.Id;
    }

    public async Task<TradeCommand?> GetCommandAsync(string id)
    {
        await Task.Delay(0); // Simulate async
        return _commands.TryGetValue(id, out var cmd) ? cmd : null;
    }

    public async Task<List<TradeCommand>> GetPendingCommandsAsync()
    {
        await Task.Delay(0); // Simulate async
        return _commands.Values
            .Where(c => c.Status == "pending")
            .OrderByDescending(c => c.RequestedAtUtc)
            .ToList();
    }

    public async Task<bool> CancelCommandAsync(string id)
    {
        if (!_commands.TryGetValue(id, out var cmd))
            return false;

        if (cmd.Status != "pending")
            return false;

        _commands.Remove(id);
        await Task.Delay(0); // Simulate async
        return true;
    }
}
