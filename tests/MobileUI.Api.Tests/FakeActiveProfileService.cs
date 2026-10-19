using MobileUI.Api.Models;
using MobileUI.Api.Services;

namespace MobileUI.Api.Tests;

/// <summary>Minimal IActiveProfileService test double — fixed paths, no persistence.</summary>
internal class FakeActiveProfileService : IActiveProfileService
{
    public FakeActiveProfileService(DaemonProfilePaths paths, string currentProfile = "Test")
    {
        CurrentPaths = paths;
        CurrentProfile = currentProfile;
        ProfileNames = new[] { currentProfile };
    }

    public IReadOnlyList<string> ProfileNames { get; }
    public string CurrentProfile { get; private set; }
    public DaemonProfilePaths CurrentPaths { get; private set; }

    public void SwitchTo(string profileName)
    {
        CurrentProfile = profileName;
    }
}
