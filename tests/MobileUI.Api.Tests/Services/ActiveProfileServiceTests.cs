using MobileUI.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Services;

[TestFixture]
public class ActiveProfileServiceTests
{
    private string _stateFilePath = null!;
    private ILogger<ActiveProfileService> _logger = null!;

    [SetUp]
    public void Setup()
    {
        _stateFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, $"active_profile_{Guid.NewGuid()}.json");
        _logger = new MockLogger<ActiveProfileService>();
    }

    [TearDown]
    public void Cleanup()
    {
        if (File.Exists(_stateFilePath))
            File.Delete(_stateFilePath);
    }

    private IConfiguration BuildConfig(string defaultProfile = "Paper")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DaemonProfiles:Paper:AppStatusPath"] = @"C:\paper\app_status.json",
                ["DaemonProfiles:Paper:JournalPath"] = @"C:\paper\live.csv",
                ["DaemonProfiles:Paper:CommandsPath"] = @"C:\paper\commands",
                ["DaemonProfiles:Live:AppStatusPath"] = @"C:\live\app_status.json",
                ["DaemonProfiles:Live:JournalPath"] = @"C:\live\live.csv",
                ["DaemonProfiles:Live:CommandsPath"] = @"C:\live\commands",
                ["DefaultProfile"] = defaultProfile,
                ["ActiveProfileStateFilePath"] = _stateFilePath,
            })
            .Build();
    }

    [Test]
    public void Constructor_WithNoPersistedState_FallsBackToDefaultProfile()
    {
        var service = new ActiveProfileService(BuildConfig("Live"), _logger);
        Assert.That(service.CurrentProfile, Is.EqualTo("Live"));
        Assert.That(service.CurrentPaths.AppStatusPath, Is.EqualTo(@"C:\live\app_status.json"));
    }

    [Test]
    public void SwitchTo_ChangesCurrentProfileAndPaths()
    {
        var service = new ActiveProfileService(BuildConfig("Paper"), _logger);
        service.SwitchTo("Live");

        Assert.That(service.CurrentProfile, Is.EqualTo("Live"));
        Assert.That(service.CurrentPaths.CommandsPath, Is.EqualTo(@"C:\live\commands"));
    }

    [Test]
    public void SwitchTo_PersistsAcrossNewInstances()
    {
        var first = new ActiveProfileService(BuildConfig("Paper"), _logger);
        first.SwitchTo("Live");

        var second = new ActiveProfileService(BuildConfig("Paper"), _logger);

        Assert.That(second.CurrentProfile, Is.EqualTo("Live"), "A fresh instance should pick up the persisted switch, not the config default");
    }

    [Test]
    public void SwitchTo_UnknownProfile_ThrowsArgumentException()
    {
        var service = new ActiveProfileService(BuildConfig("Paper"), _logger);
        Assert.Throws<ArgumentException>(() => service.SwitchTo("NotARealProfile"));
    }

    [Test]
    public void SwitchTo_IsCaseInsensitive_AndNormalizesToConfiguredCasing()
    {
        var service = new ActiveProfileService(BuildConfig("Paper"), _logger);
        service.SwitchTo("live");

        Assert.That(service.CurrentProfile, Is.EqualTo("Live"));
    }

    [Test]
    public void Constructor_DefaultProfileNotInDaemonProfiles_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ActiveProfileService(BuildConfig("NotConfigured"), _logger));
    }

    [Test]
    public void Constructor_WithNoDaemonProfilesConfigured_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DefaultProfile"] = "Paper",
                ["ActiveProfileStateFilePath"] = _stateFilePath,
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new ActiveProfileService(config, _logger));
    }

    [Test]
    public void ProfileNames_ReturnsAllConfiguredProfiles()
    {
        var service = new ActiveProfileService(BuildConfig("Paper"), _logger);
        Assert.That(service.ProfileNames, Is.EquivalentTo(new[] { "Paper", "Live" }));
    }
}
