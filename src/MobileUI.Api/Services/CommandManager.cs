using System.Text.Json;
using MobileUI.Api.Models;

namespace MobileUI.Api.Services;

public interface ICommandManager
{
    Task<string> CreateSellCommandAsync(string ticker);
    Task<string> CreateSellAllCommandAsync();
    Task<string> CreatePauseBuyingCommandAsync();
    Task<string> CreateResumeBuyingCommandAsync();
    Task<TradeCommand?> GetCommandAsync(string id);
    Task<List<TradeCommand>> GetPendingCommandsAsync();
    Task<bool> CancelCommandAsync(string id);
}

public class CommandManager : ICommandManager
{
    private readonly IActiveProfileService _activeProfile;
    private readonly IStatusReader _statusReader;
    private readonly ILogger<CommandManager> _logger;

    public CommandManager(IActiveProfileService activeProfile, IStatusReader statusReader, ILogger<CommandManager> logger)
    {
        _activeProfile = activeProfile;
        _statusReader = statusReader;
        _logger = logger;
    }

    // Commands dir follows whichever profile is currently active, which can
    // change at runtime (UI toggle), so it's resolved fresh on every call
    // rather than cached at construction.
    private string EnsureDirectories()
    {
        var commandsPath = _activeProfile.CurrentPaths.CommandsPath;
        Directory.CreateDirectory(Path.Combine(commandsPath, "pending"));
        Directory.CreateDirectory(Path.Combine(commandsPath, "processing"));
        Directory.CreateDirectory(Path.Combine(commandsPath, "results"));
        Directory.CreateDirectory(Path.Combine(commandsPath, "done"));
        return commandsPath;
    }

    public async Task<string> CreateSellCommandAsync(string ticker)
    {
        var positions = _statusReader.ReadPositions();
        var normalizedTicker = ticker.ToUpperInvariant();
        if (!positions.ContainsKey(normalizedTicker))
            throw new InvalidOperationException($"Position {ticker} not found");

        var existingCommands = await GetPendingCommandsAsync();
        if (existingCommands.Any(c => c.Action == "SELL" && c.Ticker == normalizedTicker && (c.Status == "pending" || c.Status == "queued_for_open")))
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

    public async Task<string> CreatePauseBuyingCommandAsync()
    {
        var existingCommands = await GetPendingCommandsAsync();
        if (existingCommands.Any(c => c.Action == "PAUSE_BUYING" && (c.Status == "pending" || c.Status == "queued_for_open")))
            throw new InvalidOperationException("A pending PAUSE_BUYING command already exists");

        var command = new TradeCommand
        {
            Action = "PAUSE_BUYING",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4)
        };

        return await WriteCommandAsync(command);
    }

    public async Task<string> CreateResumeBuyingCommandAsync()
    {
        var existingCommands = await GetPendingCommandsAsync();
        if (existingCommands.Any(c => c.Action == "RESUME_BUYING" && (c.Status == "pending" || c.Status == "queued_for_open")))
            throw new InvalidOperationException("A pending RESUME_BUYING command already exists");

        var command = new TradeCommand
        {
            Action = "RESUME_BUYING",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(4)
        };

        return await WriteCommandAsync(command);
    }

    private async Task<string> WriteCommandAsync(TradeCommand command)
    {
        var commandsPath = EnsureDirectories();
        var pendingPath = Path.Combine(commandsPath, "pending");
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
        var commandsPath = EnsureDirectories();
        var paths = new[]
        {
            Path.Combine(commandsPath, "pending", $"{id}.json"),
            Path.Combine(commandsPath, "processing", $"{id}.json"),
            Path.Combine(commandsPath, "results", $"{id}.json"),
            Path.Combine(commandsPath, "done", $"{id}.json")
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
        var commandsPath = EnsureDirectories();
        var pendingPath = Path.Combine(commandsPath, "pending");
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
        var commandsPath = EnsureDirectories();
        var pendingPath = Path.Combine(commandsPath, "pending", $"{id}.json");
        var donePath = Path.Combine(commandsPath, "done", $"{id}.cancelled.json");

        try
        {
            File.Move(pendingPath, donePath, overwrite: true);
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Command {CommandId} moved by daemon during cancel attempt", id);
            return false;
        }

        try
        {
            var json = await File.ReadAllTextAsync(donePath);
            var command = JsonSerializer.Deserialize<TradeCommand>(json);
            if (command != null)
            {
                command.Status = "cancelled";
                var resultJson = JsonSerializer.Serialize(command, new JsonSerializerOptions { WriteIndented = true });
                var resultsPath = Path.Combine(commandsPath, "results", $"{id}.json");
                var tempPath = resultsPath + ".tmp";

                await File.WriteAllTextAsync(tempPath, resultJson);
                File.Move(tempPath, resultsPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write results file for cancelled command {CommandId}", id);
        }

        _logger.LogInformation("Cancelled command {CommandId}", id);
        return true;
    }
}
