using System.Collections.ObjectModel;
using StrategyTradingAppUI.Maui.Models;
using StrategyTradingAppUI.Maui.Services;
using StrategyTradingAppUI.Maui.ViewModels;
using NUnit.Framework;

namespace StrategyTradingAppUI.Maui.Tests.ViewModels;

[TestFixture]
public class PositionsViewModelTests
{
    private FakeApiClient _fakeClient = null!;
    private PositionsViewModel _viewModel = null!;

    [SetUp]
    public void Setup()
    {
        _fakeClient = new FakeApiClient();
        _viewModel = new PositionsViewModel(_fakeClient, a => a());
    }

    [Test]
    public async Task RefreshAsync_PopulatesPositionsTrades_AndDaemonStatus()
    {
        _fakeClient.SetPositions(new List<Position>
        {
            new() { Ticker = "AAPL", CurrentPrice = 150.0, Quantity = 10.0 }
        });

        _fakeClient.SetRecentTrades(new List<TradeRecord>
        {
            new() { Ticker = "MSFT", Currency = "USD", Market = "NASDAQ" }
        });

        _fakeClient.SetHealth(new DaemonStatus
        {
            DaemonRunning = true,
            DryRun = false,
            HaltNewEntries = false,
            HeartbeatAgeSeconds = 5
        });

        await _viewModel.RefreshAsync();

        Assert.That(_viewModel.Positions.Count, Is.EqualTo(1));
        Assert.That(_viewModel.Positions[0].Ticker, Is.EqualTo("AAPL"));
        Assert.That(_viewModel.RecentTrades.Count, Is.EqualTo(1));
        Assert.That(_viewModel.RecentTrades[0].Ticker, Is.EqualTo("MSFT"));
        Assert.That(_viewModel.DaemonRunning, Is.True);
        Assert.That(_viewModel.DryRun, Is.False);
        Assert.That(_viewModel.HeartbeatAgeSeconds, Is.EqualTo(5));
    }

    [Test]
    public async Task RefreshAsync_WithApiFailure_SurfacesErrorState()
    {
        _fakeClient.SetShouldFail(true, "Connection refused");

        await _viewModel.RefreshAsync();

        Assert.That(_viewModel.StatusMessage, Contains.Substring("Error"));
        Assert.That(_viewModel.StatusMessage, Contains.Substring("Cannot reach server"));
        Assert.That(_viewModel.IsLoading, Is.False);
    }

    [Test]
    public async Task RefreshAsync_IsLoading_FlipsCorrectly()
    {
        var loadingStates = new List<bool>();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PositionsViewModel.IsLoading))
                loadingStates.Add(_viewModel.IsLoading);
        };

        await _viewModel.RefreshAsync();

        Assert.That(loadingStates, Contains.Item(false));
    }

    [Test]
    public async Task SubmitSellAsync_SetsIsSellInFlight()
    {
        _fakeClient.SetCommandResponse(new CommandResponse { Id = "cmd1", Status = "pending" });
        _fakeClient.SetCommand("cmd1", new TradeCommand { Id = "cmd1", Status = "filled", FillPrice = 100.0 });

        var inFlightStates = new List<bool>();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PositionsViewModel.IsSellInFlight))
                inFlightStates.Add(_viewModel.IsSellInFlight);
        };

        var task = _viewModel.SubmitSellAsync("AAPL");
        Assert.That(_viewModel.IsSellInFlight, Is.True);

        await task;
        Assert.That(_viewModel.IsSellInFlight, Is.False);
    }

    [Test]
    public async Task SubmitSellAsync_WithPendingCommand_PollsUntilTerminalState()
    {
        var pollCount = 0;
        _fakeClient.SetCommandResponse(new CommandResponse { Id = "cmd1", Status = "pending" });

        var statuses = new[] { "pending", "pending", "filled" };
        var commandStatuses = new Queue<string>(statuses);

        _fakeClient.SetCommandPoller((id) =>
        {
            pollCount++;
            var status = commandStatuses.Dequeue();
            return new TradeCommand
            {
                Id = id,
                Status = status,
                FillPrice = status == "filled" ? 100.0 : 0,
                ErrorMessage = null
            };
        });

        var response = await _viewModel.SubmitSellAsync("AAPL");

        Assert.That(pollCount, Is.GreaterThan(0));
        Assert.That(response.Status, Is.EqualTo("filled"));
        Assert.That(response.Message, Contains.Substring("Filled"));
    }

    [Test]
    public async Task SubmitSellAsync_WithErrorCommand_ReturnsErrorMessage()
    {
        _fakeClient.SetCommandResponse(new CommandResponse { Id = "cmd1", Status = "error", Message = "Insufficient shares" });

        var response = await _viewModel.SubmitSellAsync("AAPL");

        Assert.That(response.Status, Is.EqualTo("error"));
        Assert.That(_viewModel.IsSellInFlight, Is.False);
    }

    [Test]
    public async Task SubmitSellAllAsync_WorksSameAsSellAsync()
    {
        _fakeClient.SetCommandResponse(new CommandResponse { Id = "cmd-all", Status = "pending" });
        _fakeClient.SetCommand("cmd-all", new TradeCommand { Id = "cmd-all", Status = "filled" });

        var response = await _viewModel.SubmitSellAllAsync();

        Assert.That(response.Id, Is.EqualTo("cmd-all"));
        Assert.That(_viewModel.IsSellInFlight, Is.False);
    }

    [Test]
    public async Task RefreshAsync_PopulatesPausedByUser()
    {
        _fakeClient.SetHealth(new DaemonStatus
        {
            DaemonRunning = true,
            DryRun = false,
            HaltNewEntries = false,
            PausedByUser = true,
            HeartbeatAgeSeconds = 5
        });

        await _viewModel.RefreshAsync();

        Assert.That(_viewModel.PausedByUser, Is.True);
    }

    [Test]
    public async Task SubmitPauseBuyingAsync_SetsIsPauseInFlight()
    {
        _fakeClient.SetCommandResponse(new CommandResponse { Id = "pause-cmd", Status = "pending" });
        _fakeClient.SetCommand("pause-cmd", new TradeCommand { Id = "pause-cmd", Status = "filled" });

        var inFlightStates = new List<bool>();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PositionsViewModel.IsPauseInFlight))
                inFlightStates.Add(_viewModel.IsPauseInFlight);
        };

        var task = _viewModel.SubmitPauseBuyingAsync();
        Assert.That(_viewModel.IsPauseInFlight, Is.True);

        await task;
        Assert.That(_viewModel.IsPauseInFlight, Is.False);
    }

    [Test]
    public async Task SubmitPauseBuyingAsync_PollsUntilTerminalState()
    {
        var pollCount = 0;
        _fakeClient.SetCommandResponse(new CommandResponse { Id = "pause-cmd", Status = "pending" });

        var statuses = new[] { "pending", "pending", "filled" };
        var commandStatuses = new Queue<string>(statuses);

        _fakeClient.SetCommandPoller((id) =>
        {
            pollCount++;
            var status = commandStatuses.Dequeue();
            return new TradeCommand
            {
                Id = id,
                Status = status,
                ErrorMessage = null
            };
        });

        var response = await _viewModel.SubmitPauseBuyingAsync();

        Assert.That(pollCount, Is.GreaterThan(0));
        Assert.That(response.Status, Is.EqualTo("filled"));
        Assert.That(response.Message, Contains.Substring("paused"));
    }

    [Test]
    public async Task SubmitPauseBuyingAsync_HealthRefreshFailure_StillReturnsFilled()
    {
        _fakeClient.SetCommandResponse(new CommandResponse { Id = "pause-cmd", Status = "pending" });
        _fakeClient.SetCommand("pause-cmd", new TradeCommand { Id = "pause-cmd", Status = "filled" });
        _fakeClient.SetHealthShouldFail(true);

        var response = await _viewModel.SubmitPauseBuyingAsync();

        Assert.That(response.Status, Is.EqualTo("filled"));
        Assert.That(_viewModel.IsPauseInFlight, Is.False);
    }

    [Test]
    public async Task SubmitResumeBuyingAsync_WorksSameAsPauseBuying()
    {
        _fakeClient.SetCommandResponse(new CommandResponse { Id = "resume-cmd", Status = "pending" });
        _fakeClient.SetCommand("resume-cmd", new TradeCommand { Id = "resume-cmd", Status = "filled" });

        var response = await _viewModel.SubmitResumeBuyingAsync();

        Assert.That(response.Id, Is.EqualTo("resume-cmd"));
        Assert.That(_viewModel.IsPauseInFlight, Is.False);
        Assert.That(response.Message, Contains.Substring("resumed"));
    }

    [Test]
    public async Task CancelPendingAsync_WithSuccess_ReturnsNull()
    {
        _fakeClient.SetCancelResult("cmd1", null);

        var result = await _viewModel.CancelPendingAsync("cmd1");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CancelPendingAsync_WithConflict_ReturnsErrorMessage()
    {
        _fakeClient.SetCancelResult("cmd1", "Command is already executing");

        var result = await _viewModel.CancelPendingAsync("cmd1");

        Assert.That(result, Contains.Substring("already executing"));
    }

    [Test]
    public async Task CancelPendingAsync_UpdatesPendingCommands()
    {
        _fakeClient.SetCommands(new List<TradeCommand>
        {
            new() { Id = "cmd1", Status = "pending" }
        });
        _fakeClient.SetCancelResult("cmd1", null);

        _fakeClient.SetCommands(new List<TradeCommand>());

        await _viewModel.CancelPendingAsync("cmd1");

        Assert.That(_viewModel.PendingCommands.Count, Is.EqualTo(0));
    }

    [Test]
    public void GetMarketStatus_WithValidMarket_ReturnsStatus()
    {
        var health = new DaemonStatus
        {
            Markets = new Dictionary<string, MarketStatus>
            {
                ["ftse"] = new() { InTradingHours = true, LastCycleHour = 14 }
            }
        };

        _fakeClient.SetHealth(health);
        _viewModel.RefreshAsync().Wait();

        var status = _viewModel.GetMarketStatus("ftse");

        Assert.That(status, Is.Not.Null);
        Assert.That(status!.InTradingHours, Is.True);
    }

    [Test]
    public void GetMarketStatus_WithMissingMarket_ReturnsNull()
    {
        var health = new DaemonStatus { Markets = new Dictionary<string, MarketStatus>() };
        _fakeClient.SetHealth(health);
        _viewModel.RefreshAsync().Wait();

        var status = _viewModel.GetMarketStatus("unknown");

        Assert.That(status, Is.Null);
    }
}

internal class FakeApiClient : IApiClient
{
    private List<Position> _positions = new();
    private List<TradeRecord> _trades = new();
    private DaemonStatus _health = new();
    private List<TradeCommand> _commands = new();
    private CommandResponse _commandResponse = new();
    private Dictionary<string, TradeCommand> _commandById = new();
    private Dictionary<string, string?> _cancelResults = new();
    private bool _shouldFail = false;
    private bool _healthShouldFail = false;
    private string _failMessage = "";
    private Func<string, TradeCommand>? _commandPoller;

    public void SetPositions(List<Position> positions) => _positions = positions;
    public void SetRecentTrades(List<TradeRecord> trades) => _trades = trades;
    public void SetHealth(DaemonStatus health) => _health = health;
    public void SetCommands(List<TradeCommand> commands) => _commands = commands;
    public void SetCommandResponse(CommandResponse response) => _commandResponse = response;
    public void SetCommand(string id, TradeCommand command) => _commandById[id] = command;
    public void SetCancelResult(string id, string? result) => _cancelResults[id] = result;
    public void SetShouldFail(bool fail, string message) { _shouldFail = fail; _failMessage = message; }
    public void SetHealthShouldFail(bool fail) => _healthShouldFail = fail;
    public void SetCommandPoller(Func<string, TradeCommand> poller) => _commandPoller = poller;

    public Task<List<Position>> GetPositionsAsync()
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);
        return Task.FromResult(_positions);
    }

    public Task<List<TradeRecord>> GetRecentTradesAsync(int count = 20)
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);
        return Task.FromResult(_trades.TakeLast(count).ToList());
    }

    public Task<DaemonStatus> GetHealthAsync()
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);
        if (_healthShouldFail) throw new InvalidOperationException("Health endpoint unavailable");
        return Task.FromResult(_health);
    }

    public async Task<CommandResponse> SellAsync(string ticker)
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);
        await Task.Yield();
        return _commandResponse;
    }

    public async Task<CommandResponse> SellAllAsync()
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);
        await Task.Yield();
        return _commandResponse;
    }

    public async Task<CommandResponse> PauseBuyingAsync()
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);
        await Task.Yield();
        return _commandResponse;
    }

    public async Task<CommandResponse> ResumeBuyingAsync()
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);
        await Task.Yield();
        return _commandResponse;
    }

    public async Task<List<TradeCommand>> GetCommandsAsync()
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);
        await Task.Yield();
        return _commands;
    }

    public async Task<TradeCommand?> GetCommandAsync(string id)
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);

        if (_commandPoller != null)
        {
            await Task.Yield();
            return _commandPoller(id);
        }

        await Task.Yield();
        return _commandById.TryGetValue(id, out var cmd) ? cmd : null;
    }

    public async Task<string?> CancelCommandAsync(string id)
    {
        if (_shouldFail) throw new InvalidOperationException(_failMessage);

        if (_cancelResults.TryGetValue(id, out var result))
        {
            await Task.Yield();
            return result;
        }

        await Task.Yield();
        return null;
    }

    public void SetBaseUrl(string url) { }
    public void SetCertificateThumbprint(string thumbprint) { }
    public async Task SetApiKeyAsync(string apiKey) { await Task.Delay(0); }
    public Task<string> GetApiKeyAsync() => Task.FromResult("");
    public string GetBaseUrl() => "http://test";
}
