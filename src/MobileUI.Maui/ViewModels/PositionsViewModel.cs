using System.Collections.ObjectModel;
using System.Windows.Input;
using MobileUI.Maui.Models;
using MobileUI.Maui.Services;

namespace MobileUI.Maui.ViewModels;

public class PositionsViewModel : BindableObject
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private string _statusMessage = "Loading...";
    private bool _daemonRunning;
    private bool _dryRun;
    private bool _haltNewEntries;
    private int? _heartbeatAgeSeconds;
    private bool _isSellInFlight;
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
        set { _daemonRunning = value; OnPropertyChanged(); }
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

    public int? HeartbeatAgeSeconds
    {
        get => _heartbeatAgeSeconds;
        set { _heartbeatAgeSeconds = value; OnPropertyChanged(); }
    }

    public bool IsSellInFlight
    {
        get => _isSellInFlight;
        set { _isSellInFlight = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Position> Positions { get; } = new();
    public ObservableCollection<TradeRecord> RecentTrades { get; } = new();
    public ObservableCollection<TradeCommand> PendingCommands { get; } = new();

    public ICommand RefreshCommand { get; }

    public PositionsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
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
            MainThread.BeginInvokeOnMainThread(() =>
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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DaemonRunning = health.DaemonRunning;
                DryRun = health.DryRun;
                HaltNewEntries = health.HaltNewEntries;
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
            MainThread.BeginInvokeOnMainThread(() =>
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
            MainThread.BeginInvokeOnMainThread(() =>
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
