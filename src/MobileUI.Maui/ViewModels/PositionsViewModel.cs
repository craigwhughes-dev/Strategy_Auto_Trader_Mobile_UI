using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using StrategyTradingAppUI.Maui.Models;
using StrategyTradingAppUI.Maui.Services;

namespace StrategyTradingAppUI.Maui.ViewModels;

public class PositionsViewModel : BindableObject
{
    private readonly IApiClient _apiClient;
    private readonly Action<Action> _dispatcher;

    private bool _isLoading;
    private string _statusMessage = "Loading...";
    private bool _daemonRunning;
    private bool _dryRun;
    private bool _haltNewEntries;
    private bool _pausedByUser;
    private int? _heartbeatAgeSeconds;
    private bool _isSellInFlight;
    private bool _isPauseInFlight;
    private DaemonStatus? _lastHealth;

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool DaemonRunning
    {
        get => _daemonRunning;
        set { _daemonRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(DaemonStatusDetail)); }
    }

    public bool DryRun
    {
        get => _dryRun;
        set { _dryRun = value; OnPropertyChanged(); }
    }

    public bool HaltNewEntries
    {
        get => _haltNewEntries;
        set { _haltNewEntries = value; OnPropertyChanged(); }
    }

    public bool PausedByUser
    {
        get => _pausedByUser;
        set { _pausedByUser = value; OnPropertyChanged(); }
    }

    public int? HeartbeatAgeSeconds
    {
        get => _heartbeatAgeSeconds;
        set { _heartbeatAgeSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(DaemonStatusDetail)); }
    }

    public string DaemonStatusDetail
    {
        get
        {
            if (DaemonRunning || _heartbeatAgeSeconds is not int age)
                return string.Empty;

            var span = TimeSpan.FromSeconds(age);
            var formatted = span.TotalMinutes >= 1
                ? $"{(int)span.TotalMinutes}m {span.Seconds}s"
                : $"{span.Seconds}s";
            return $"Last seen {formatted} ago";
        }
    }

    public bool IsSellInFlight
    {
        get => _isSellInFlight;
        set { _isSellInFlight = value; OnPropertyChanged(); }
    }

    public bool IsPauseInFlight
    {
        get => _isPauseInFlight;
        set { _isPauseInFlight = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Position> Positions { get; } = new();
    public ObservableCollection<TradeRecord> RecentTrades { get; } = new();
    public ObservableCollection<TradeCommand> PendingCommands { get; } = new();

    public ICommand RefreshCommand { get; }

    public PositionsViewModel(IApiClient apiClient, Action<Action>? dispatcher = null)
    {
        _apiClient = apiClient;
        _dispatcher = dispatcher ?? MainThread.BeginInvokeOnMainThread;
        RefreshCommand = new Command(async () => await RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading...";
        try
        {
            var posTask = LoadPositionsAsync();
            var healthTask = LoadHealthAsync();
            var tradesTask = LoadRecentTradesAsync();
            var commandsTask = LoadPendingCommandsAsync();

            await Task.WhenAll(posTask, healthTask, tradesTask, commandsTask);
            StatusMessage = "Updated";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Refresh error: {ex}");
            StatusMessage = $"Error: Cannot reach server";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPositionsAsync()
    {
        try
        {
            var positions = await _apiClient.GetPositionsAsync();
            _dispatcher(() =>
            {
                Positions.Clear();
                foreach (var pos in positions)
                {
                    Positions.Add(pos);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading positions: {ex}");
            throw;
        }
    }

    private async Task LoadHealthAsync()
    {
        try
        {
            var health = await _apiClient.GetHealthAsync();
            _lastHealth = health;
            _dispatcher(() =>
            {
                DaemonRunning = health.DaemonRunning;
                DryRun = health.DryRun;
                HaltNewEntries = health.HaltNewEntries;
                PausedByUser = health.PausedByUser;
                HeartbeatAgeSeconds = health.HeartbeatAgeSeconds;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading health: {ex}");
            throw;
        }
    }

    private async Task LoadRecentTradesAsync()
    {
        try
        {
            var trades = await _apiClient.GetRecentTradesAsync(count: 5);
            _dispatcher(() =>
            {
                RecentTrades.Clear();
                foreach (var trade in trades)
                {
                    RecentTrades.Add(trade);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading recent trades: {ex}");
            throw;
        }
    }

    private async Task LoadPendingCommandsAsync()
    {
        try
        {
            var commands = await _apiClient.GetCommandsAsync();
            _dispatcher(() =>
            {
                PendingCommands.Clear();
                foreach (var cmd in commands)
                {
                    PendingCommands.Add(cmd);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading pending commands: {ex}");
        }
    }

    public async Task<CommandResponse> SubmitSellAsync(string ticker)
    {
        IsSellInFlight = true;
        try
        {
            var response = await _apiClient.SellAsync(ticker);
            if (response.Status == "error" || string.IsNullOrEmpty(response.Id))
                return response;

            await LoadPendingCommandsAsync();
            var finalCommand = await PollCommandStatusAsync(response.Id);

            if (finalCommand != null)
            {
                return new CommandResponse
                {
                    Id = finalCommand.Id,
                    Status = finalCommand.Status,
                    Message = finalCommand.Status switch
                    {
                        "filled" => $"Filled at {finalCommand.FillPrice:F2}",
                        "error" => finalCommand.ErrorMessage ?? "Unknown error",
                        _ => response.Message
                    }
                };
            }

            return response;
        }
        finally
        {
            IsSellInFlight = false;
        }
    }

    public async Task<CommandResponse> SubmitSellAllAsync()
    {
        IsSellInFlight = true;
        try
        {
            var response = await _apiClient.SellAllAsync();
            if (response.Status == "error" || string.IsNullOrEmpty(response.Id))
                return response;

            await LoadPendingCommandsAsync();
            var finalCommand = await PollCommandStatusAsync(response.Id);

            if (finalCommand != null)
            {
                return new CommandResponse
                {
                    Id = finalCommand.Id,
                    Status = finalCommand.Status,
                    Message = finalCommand.Status switch
                    {
                        "filled" => "Filled",
                        "error" => finalCommand.ErrorMessage ?? "Unknown error",
                        _ => response.Message
                    }
                };
            }

            return response;
        }
        finally
        {
            IsSellInFlight = false;
        }
    }

    public async Task<CommandResponse> SubmitPauseBuyingAsync()
    {
        IsPauseInFlight = true;
        try
        {
            var response = await _apiClient.PauseBuyingAsync();
            if (response.Status == "error" || string.IsNullOrEmpty(response.Id))
                return response;

            await LoadPendingCommandsAsync();
            var finalCommand = await PollCommandStatusAsync(response.Id);
            if (finalCommand != null)
            {
                var result = new CommandResponse
                {
                    Id = finalCommand.Id,
                    Status = finalCommand.Status,
                    Message = finalCommand.Status switch
                    {
                        "filled" => "Buying paused",
                        "error" => finalCommand.ErrorMessage ?? "Unknown error",
                        _ => response.Message
                    }
                };
                if (result.Status == "filled")
                {
                    try
                    {
                        await LoadHealthAsync();
                    }
                    catch (Exception ex)
                    {
                        // Command outcome already determined; pill corrects on next refresh
                        System.Diagnostics.Debug.WriteLine($"Health refresh after pause failed: {ex}");
                    }
                }
                return result;
            }
            return response;
        }
        finally
        {
            IsPauseInFlight = false;
        }
    }

    public async Task<CommandResponse> SubmitResumeBuyingAsync()
    {
        IsPauseInFlight = true;
        try
        {
            var response = await _apiClient.ResumeBuyingAsync();
            if (response.Status == "error" || string.IsNullOrEmpty(response.Id))
                return response;

            await LoadPendingCommandsAsync();
            var finalCommand = await PollCommandStatusAsync(response.Id);
            if (finalCommand != null)
            {
                var result = new CommandResponse
                {
                    Id = finalCommand.Id,
                    Status = finalCommand.Status,
                    Message = finalCommand.Status switch
                    {
                        "filled" => "Buying resumed",
                        "error" => finalCommand.ErrorMessage ?? "Unknown error",
                        _ => response.Message
                    }
                };
                if (result.Status == "filled")
                {
                    try
                    {
                        await LoadHealthAsync();
                    }
                    catch (Exception ex)
                    {
                        // Command outcome already determined; pill corrects on next refresh
                        System.Diagnostics.Debug.WriteLine($"Health refresh after resume failed: {ex}");
                    }
                }
                return result;
            }
            return response;
        }
        finally
        {
            IsPauseInFlight = false;
        }
    }

    private async Task<TradeCommand?> PollCommandStatusAsync(string commandId)
    {
        var startTime = DateTime.UtcNow;
        var pollInterval = TimeSpan.FromSeconds(3);
        var timeout = TimeSpan.FromSeconds(60);
        TradeCommand? lastCommand = null;

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                var command = await _apiClient.GetCommandAsync(commandId);
                if (command == null)
                    break;

                lastCommand = command;

                if (command.Status == "filled" || command.Status == "error" ||
                    command.Status == "expired" || command.Status == "cancelled")
                {
                    await LoadPendingCommandsAsync();
                    await LoadPositionsAsync();
                    break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error polling command status: {ex}");
            }

            await Task.Delay(pollInterval);
        }

        await LoadPendingCommandsAsync();
        return lastCommand;
    }

    public async Task<string?> CancelPendingAsync(string id)
    {
        try
        {
            var result = await _apiClient.CancelCommandAsync(id);
            await LoadPendingCommandsAsync();
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cancelling command: {ex}");
            return ex.Message;
        }
    }

    public MarketStatus? GetMarketStatus(string market)
    {
        if (_lastHealth?.Markets == null)
            return null;

        _lastHealth.Markets.TryGetValue(market, out var status);
        return status;
    }
}
