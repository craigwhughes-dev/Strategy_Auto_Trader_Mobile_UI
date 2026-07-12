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
    private List<TradeRecord> _recentTrades = new();

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

    public ObservableCollection<Position> Positions { get; } = new();
    public ObservableCollection<TradeRecord> RecentTrades { get; } = new();

    public ICommand RefreshCommand { get; }

    public PositionsViewModel() : this(new ApiClient())
    {
    }

    public PositionsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new Command(async () => await RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await Task.WhenAll(
                LoadPositionsAsync(),
                LoadHealthAsync()
            );
            StatusMessage = "Updated";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
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
        }
    }

    private async Task LoadHealthAsync()
    {
        try
        {
            var health = await _apiClient.GetHealthAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DaemonRunning = health.DaemonRunning;
                DryRun = health.DryRun;
                HaltNewEntries = health.HaltNewEntries;
                HeartbeatAgeSeconds = health.HeartbeatAgeSeconds;

                var recentTrades = _recentTrades;
                RecentTrades.Clear();
                foreach (var trade in recentTrades.Take(5))
                {
                    RecentTrades.Add(trade);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading health: {ex}");
        }
    }
}
