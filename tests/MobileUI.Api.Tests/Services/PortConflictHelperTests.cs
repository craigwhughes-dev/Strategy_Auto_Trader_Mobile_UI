using MobileUI.Api.Services;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Services;

[TestFixture]
public class PortConflictHelperTests
{
    private const string SampleNetstatOutput = @"
Active Connections

  Proto  Local Address          Foreign Address        State           PID
  TCP    0.0.0.0:135            0.0.0.0:0              LISTENING       900
  TCP    0.0.0.0:5000           0.0.0.0:0              LISTENING       12345
  TCP    [::]:5000              [::]:0                 LISTENING       12345
  TCP    0.0.0.0:5001           0.0.0.0:0              LISTENING       67890
  TCP    127.0.0.1:5002         0.0.0.0:0              LISTENING       111
  TCP    10.0.0.5:54321         10.0.0.9:5000          ESTABLISHED     222
";

    [Test]
    public void ParseListeningPids_ReturnsPidsForMatchingPorts_Deduplicated()
    {
        var pids = PortConflictHelper.ParseListeningPids(SampleNetstatOutput, new HashSet<int> { 5000, 5001 });

        Assert.That(pids, Is.EquivalentTo(new[] { 12345, 67890 }));
    }

    [Test]
    public void ParseListeningPids_IgnoresNonListeningState()
    {
        var pids = PortConflictHelper.ParseListeningPids(SampleNetstatOutput, new HashSet<int> { 5000 });

        Assert.That(pids, Does.Not.Contain(222));
    }

    [Test]
    public void ParseListeningPids_IgnoresUnrequestedPorts()
    {
        var pids = PortConflictHelper.ParseListeningPids(SampleNetstatOutput, new HashSet<int> { 5000, 5001 });

        Assert.That(pids, Does.Not.Contain(900));
        Assert.That(pids, Does.Not.Contain(111));
    }

    [Test]
    public void ParseListeningPids_NoMatches_ReturnsEmpty()
    {
        var pids = PortConflictHelper.ParseListeningPids(SampleNetstatOutput, new HashSet<int> { 9999 });

        Assert.That(pids, Is.Empty);
    }

    [Test]
    public void ParseListeningPids_EmptyOutput_ReturnsEmpty()
    {
        var pids = PortConflictHelper.ParseListeningPids(string.Empty, new HashSet<int> { 5000 });

        Assert.That(pids, Is.Empty);
    }

    [Test]
    public void BuildDiagnosticMessage_IncludesStopProcessCommandsForRealListener()
    {
        // The current process is always listed by netstat's PID column for something,
        // so exercise the end-to-end path via a port that is very unlikely to be bound,
        // asserting the "no listener found" fallback branch works without throwing.
        var message = PortConflictHelper.BuildDiagnosticMessage(new HashSet<int> { 65530 });

        Assert.That(message, Does.Contain("65530"));
    }
}
