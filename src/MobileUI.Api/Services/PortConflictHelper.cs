using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MobileUI.Api.Services;

public static class PortConflictHelper
{
    private static readonly Regex ListeningLineRegex = new(
        @"^\s*TCP\s+\S+:(?<port>\d+)\s+\S+\s+LISTENING\s+(?<pid>\d+)\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Parses `netstat -ano` output and returns the PIDs listening on any of the given ports.</summary>
    public static IReadOnlyList<int> ParseListeningPids(string netstatOutput, IReadOnlySet<int> ports)
    {
        var pids = new List<int>();
        foreach (Match match in ListeningLineRegex.Matches(netstatOutput))
        {
            var port = int.Parse(match.Groups["port"].Value);
            if (!ports.Contains(port))
            {
                continue;
            }

            var pid = int.Parse(match.Groups["pid"].Value);
            if (!pids.Contains(pid))
            {
                pids.Add(pid);
            }
        }

        return pids;
    }

    /// <summary>Runs `netstat -ano`, finds the PIDs bound to <paramref name="ports"/>, and builds a message with copy-pasteable PowerShell commands to inspect/kill them.</summary>
    public static string BuildDiagnosticMessage(IReadOnlySet<int> ports)
    {
        var portList = string.Join("/", ports);
        List<int> pids;
        try
        {
            var netstatOutput = RunNetstat();
            pids = ParseListeningPids(netstatOutput, ports).ToList();
        }
        catch (Exception ex)
        {
            return $"Could not identify the process holding port {portList} (netstat failed: {ex.Message}).{Environment.NewLine}"
                + $"Find it manually with: netstat -ano | findstr :{ports.First()}";
        }

        if (pids.Count == 0)
        {
            return $"No listening process found on port {portList} via netstat — it may have just released the port. Try running again.";
        }

        var lines = new List<string>
        {
            $"Another process is bound to port {portList}:"
        };

        foreach (var pid in pids)
        {
            var name = TryGetProcessName(pid);
            lines.Add(name is null
                ? $"  PID {pid} (process details unavailable)"
                : $"  PID {pid} ({name})");
        }

        lines.Add("");
        lines.Add("To stop it, run in PowerShell:");
        foreach (var pid in pids)
        {
            lines.Add($"  Stop-Process -Id {pid} -Force");
        }

        lines.Add("");
        lines.Add("If it's the MobileUI.Api Scheduled Task, stop that instead of killing the PID directly:");
        lines.Add("  Stop-ScheduledTask -TaskName 'MobileUI.Api'");

        return string.Join(Environment.NewLine, lines);
    }

    private static string RunNetstat()
    {
        var psi = new ProcessStartInfo("netstat", "-ano")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start netstat");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private static string? TryGetProcessName(int pid)
    {
        try
        {
            return Process.GetProcessById(pid).ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
