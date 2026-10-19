using System.Text.Json;
using MobileUI.Api.Models;

namespace MobileUI.Api.Services;

public interface IActiveProfileService
{
    IReadOnlyList<string> ProfileNames { get; }
    string CurrentProfile { get; }
    DaemonProfilePaths CurrentPaths { get; }
    void SwitchTo(string profileName);
}

/// <summary>
/// Holds which daemon (e.g. paper vs live/real-money) the API currently reads
/// state from and writes trade commands to. The selection is config-seeded but
/// user-switchable at runtime via the UI, and persisted to disk so a restart
/// doesn't silently fall back to whichever profile the config file names —
/// that fallback only kicks in the first time this instance has ever run.
/// </summary>
public class ActiveProfileService : IActiveProfileService
{
    private readonly Dictionary<string, DaemonProfilePaths> _profiles;
    private readonly string _stateFilePath;
    private readonly ILogger<ActiveProfileService> _logger;
    private readonly object _lock = new();
    private string _currentProfile;

    public ActiveProfileService(IConfiguration configuration, ILogger<ActiveProfileService> logger)
    {
        _logger = logger;
        _profiles = LoadProfiles(configuration);

        if (_profiles.Count == 0)
            throw new InvalidOperationException("DaemonProfiles not configured");

        var defaultProfile = configuration["DefaultProfile"]
            ?? throw new InvalidOperationException("DefaultProfile not configured");

        if (!_profiles.ContainsKey(defaultProfile))
            throw new InvalidOperationException(
                $"DefaultProfile '{defaultProfile}' is not one of the configured DaemonProfiles ({string.Join(", ", _profiles.Keys)})");

        _stateFilePath = configuration["ActiveProfileStateFilePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "state", "active_profile.json");

        _currentProfile = LoadPersistedProfile() ?? Canonicalize(defaultProfile);
        _logger.LogInformation("Active daemon profile: {Profile}", _currentProfile);
    }

    public IReadOnlyList<string> ProfileNames => _profiles.Keys.ToList();

    public string CurrentProfile
    {
        get { lock (_lock) return _currentProfile; }
    }

    public DaemonProfilePaths CurrentPaths
    {
        get { lock (_lock) return _profiles[_currentProfile]; }
    }

    public void SwitchTo(string profileName)
    {
        if (!_profiles.ContainsKey(profileName))
            throw new ArgumentException(
                $"Unknown profile '{profileName}'. Known profiles: {string.Join(", ", _profiles.Keys)}");

        var canonicalName = Canonicalize(profileName);

        lock (_lock)
        {
            _currentProfile = canonicalName;
            PersistCurrentProfile(canonicalName);
        }

        _logger.LogWarning("Switched active daemon profile to {Profile}", canonicalName);
    }

    private static Dictionary<string, DaemonProfilePaths> LoadProfiles(IConfiguration configuration)
    {
        var profiles = new Dictionary<string, DaemonProfilePaths>(StringComparer.OrdinalIgnoreCase);

        foreach (var profileSection in configuration.GetSection("DaemonProfiles").GetChildren())
        {
            profiles[profileSection.Key] = new DaemonProfilePaths
            {
                AppStatusPath = profileSection["AppStatusPath"]
                    ?? throw new InvalidOperationException($"DaemonProfiles:{profileSection.Key}:AppStatusPath not configured"),
                JournalPath = profileSection["JournalPath"]
                    ?? throw new InvalidOperationException($"DaemonProfiles:{profileSection.Key}:JournalPath not configured"),
                CommandsPath = profileSection["CommandsPath"]
                    ?? throw new InvalidOperationException($"DaemonProfiles:{profileSection.Key}:CommandsPath not configured"),
            };
        }

        return profiles;
    }

    // Config keys ("Paper", "Live") carry the display casing. A persisted or
    // user-supplied name is matched case-insensitively but stored/returned as
    // the config's own casing, so the UI always shows a consistent label.
    private string Canonicalize(string profileName) =>
        _profiles.Keys.First(k => string.Equals(k, profileName, StringComparison.OrdinalIgnoreCase));

    private string? LoadPersistedProfile()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
                return null;

            var json = File.ReadAllText(_stateFilePath);
            using var doc = JsonDocument.Parse(json);
            var name = doc.RootElement.GetProperty("profile").GetString();
            if (!string.IsNullOrEmpty(name) && _profiles.ContainsKey(name))
                return Canonicalize(name);

            _logger.LogWarning("Persisted active profile {Profile} at {Path} is not a known profile; ignoring", name, _stateFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read persisted active profile from {Path}", _stateFilePath);
        }

        return null;
    }

    private void PersistCurrentProfile(string profileName)
    {
        try
        {
            var dir = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(new { profile = profileName });
            var tempPath = _stateFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _stateFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist active profile to {Path}", _stateFilePath);
        }
    }
}
