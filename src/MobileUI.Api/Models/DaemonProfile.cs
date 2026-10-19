namespace MobileUI.Api.Models;

public class DaemonProfilePaths
{
    public string AppStatusPath { get; set; } = string.Empty;
    public string JournalPath { get; set; } = string.Empty;
    public string CommandsPath { get; set; } = string.Empty;
}

public class SwitchProfileRequest
{
    public string Profile { get; set; } = string.Empty;
}
