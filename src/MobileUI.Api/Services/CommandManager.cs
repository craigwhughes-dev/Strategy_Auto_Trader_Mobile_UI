using System.Text.Json;
using MobileUI.Api.Models;

namespace MobileUI.Api.Services;

public interface ICommandManager
{
    Task<string> CreateSellCommandAsync(string ticker);
    Task<string> CreateSellAllCommandAsync();
    Task<TradeCommand?> GetCommandAsync(string id);
    Task<List<TradeCommand>> GetPendingCommandsAsync();
    Task<bool> CancelCommandAsync(string id);
}

public class CommandManager : ICommandManager
{
    private readonly string _commandsPath;
    private readonly IStatusReader _statusReader;
    private readonly ILogger<CommandManager> _logger;

    public CommandManager(IConfiguration config, IStatusReader statusReader, ILogger<CommandManager> logger)
    {
        _commandsPath = config["DaemonState:CommandsPath"]
            ?? throw new InvalidOperationException("DaemonState:CommandsPath not configured");
        _statusReader = statusReader;
        _logger = logger;
        EnsureDirectories();
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_commandsPath, "pending"));
        Directory.CreateDirectory(Path.Combine(_commandsPath, "processing"));
        Directory.CreateDirectory(Path.Combine(_commandsPath, "results"));
        Directory.CreateDirectory(Path.Combine(_commandsPath, "done"));
    }

    public async Task<string> CreateSellCommandAsync(string ticker)
    {
        var positions = _statusReader.ReadPositions();
        var normalizedTicker = ticker.ToUpperInvariant();
        if (!positions.ContainsKey(normalizedTicker))
            throw new InvalidOperationException($"Position {ticker} not found");

        var existingCommands = await GetPendingCommandsAsync();
        if (existingCommands.Any(c => c.Action == "SELL" && c.Ticker == normalizedTicker && c.Status == "pending"))
            throw new InvalidOperationException($"A pending SELL command for {ticker} already exists");

        var command = new TradeCommand
        {
            Action = "SELL",
            Ticker = normalizedTicker,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4)
        };

        return await WriteCommandAsync(command);
    }

    public async Task<string> CreateSellAllCommandAsync()
    {
        var command = new TradeCommand
        {
            Action = "SELL_ALL",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4)
        };

        return await WriteCommandAsync(command);
    }

    private async Task<string> WriteCommandAsync(TradeCommand command)
    {
        var pendingPath = Path.Combine(_commandsPath, "pending");
        var filePath = Path.Combine(pendingPath, $"{command.Id}.json");

        var json = JsonSerializer.Serialize(command, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = filePath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);

        _logger.LogInformation("Created command {CommandId} for {Action} {Ticker}", command.Id, command.Action, command.Ticker ?? "ALL");
        return command.Id;
    }

    public async Task<TradeCommand?> GetCommandAsync(string id)
    {
        var paths = new[]
        {
            Path.Combine(_commandsPath, "pending", $"{id}.json"),
            Path.Combine(_commandsPath, "processing", $"{id}.json"),
            Path.Combine(_commandsPath, "results", $"{id}.json"),
            Path.Combine(_commandsPath, "done", $"{id}.json")
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<TradeCommand>(json);
            }
        }

        return null;
    }

    public async Task<List<TradeCommand>> GetPendingCommandsAsync()
    {
        var pendingPath = Path.Combine(_commandsPath, "pending");
        var commands = new List<TradeCommand>();

        if (!Directory.Exists(pendingPath))
            return commands;

        foreach (var file in Directory.GetFiles(pendingPath, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var cmd = JsonSerializer.Deserialize<TradeCommand>(json);
                if (cmd != null)
                    commands.Add(cmd);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read command file {File}", file);
            }
        }

        return commands.OrderByDescending(c => c.RequestedAtUtc).ToList();
    }

    public async Task<bool> CancelCommandAsync(string id)
    {
        var pendingPath = Path.Combine(_commandsPath, "pending", $"{id}.json");

        if (!File.Exists(pendingPath))
        {
            _logger.LogWarning("Cannot cancel command {CommandId}: not in pending state", id);
            return false;
        }

        try
        {
            File.Delete(pendingPath);
            _logger.LogInformation("Cancelled command {CommandId}", id);
            return true;
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Command {CommandId} moved by daemon during cancel attempt", id);
            return false;
        }
    }
}
