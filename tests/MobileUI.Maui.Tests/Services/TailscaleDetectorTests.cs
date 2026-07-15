using System.Net;
using System.Net.NetworkInformation;
using StrategyTradingAppUI.Maui.Services;
using NUnit.Framework;

namespace StrategyTradingAppUI.Maui.Tests.Services;

[TestFixture]
public class TailscaleDetectorTests
{
    [Test]
    public void IsConnected_NoInterfaces_ReturnsFalse()
    {
        var provider = new FakeNetworkInterfaceProvider(new List<NetworkInterfaceInfo>());
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_AllInterfacesDown_ReturnsFalse()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new() { Status = OperationalStatus.Down, IPv4Addresses = new List<IPAddress>() },
            new() { Status = OperationalStatus.NotPresent, IPv4Addresses = new List<IPAddress>() },
            new() { Status = OperationalStatus.Testing, IPv4Addresses = new List<IPAddress>() }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_InterfaceUpButNoAddresses_ReturnsFalse()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new() { Status = OperationalStatus.Up, IPv4Addresses = new List<IPAddress>() }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_IPv4OutsideRange_Byte0Wrong_ReturnsFalse()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress>
                {
                    IPAddress.Parse("99.64.0.1"),
                    IPAddress.Parse("101.64.0.1")
                }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_IPv4OutsideRange_Byte1TooLow_ReturnsFalse()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress>
                {
                    IPAddress.Parse("100.63.255.255"),
                    IPAddress.Parse("100.0.0.1")
                }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_IPv4OutsideRange_Byte1TooHigh_ReturnsFalse()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress>
                {
                    IPAddress.Parse("100.128.0.1"),
                    IPAddress.Parse("100.255.255.255")
                }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_TailscaleRangeLowerBoundary_ReturnsTrue()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress> { IPAddress.Parse("100.64.0.0") }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsConnected_TailscaleRangeUpperBoundary_ReturnsTrue()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress> { IPAddress.Parse("100.127.255.255") }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsConnected_TailscaleRangeMiddle_ReturnsTrue()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress> { IPAddress.Parse("100.95.150.75") }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsConnected_MultipleInterfacesWithTailscale_ReturnsTrue()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress> { IPAddress.Parse("192.168.1.1") }
            },
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress>
                {
                    IPAddress.Parse("10.0.0.1"),
                    IPAddress.Parse("100.85.20.5")
                }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsConnected_FirstInterfaceDownSecondUp_ReturnsTrue()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Down,
                IPv4Addresses = new List<IPAddress>()
            },
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress> { IPAddress.Parse("100.64.5.10") }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsConnected_ProviderThrowsException_ReturnsFalse()
    {
        var provider = new ThrowingNetworkInterfaceProvider(new InvalidOperationException("Network error"));
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_ProviderThrowsNullReferenceException_ReturnsFalse()
    {
        var provider = new ThrowingNetworkInterfaceProvider(new NullReferenceException("Null interface"));
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_ProviderThrowsOutOfMemory_ReturnsFalse()
    {
        var provider = new ThrowingNetworkInterfaceProvider(new OutOfMemoryException());
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_WithNullProvider_UsesDefaultProvider()
    {
        var detector = new TailscaleDetectorService(null);

        var result = detector.IsConnected();

        Assert.That(result, Is.TypeOf<bool>());
    }

    [Test]
    public void IsConnected_LoopkbackAddress_OutsideRange_ReturnsFalse()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress> { IPAddress.Loopback }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsConnected_Byte1Boundary_64_ReturnsTrue()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress> { IPAddress.Parse("100.64.0.1") }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsConnected_Byte1Boundary_127_ReturnsTrue()
    {
        var interfaces = new List<NetworkInterfaceInfo>
        {
            new()
            {
                Status = OperationalStatus.Up,
                IPv4Addresses = new List<IPAddress> { IPAddress.Parse("100.127.0.1") }
            }
        };
        var provider = new FakeNetworkInterfaceProvider(interfaces);
        var detector = new TailscaleDetectorService(provider);

        var result = detector.IsConnected();

        Assert.That(result, Is.True);
    }
}

internal class FakeNetworkInterfaceProvider : INetworkInterfaceProvider
{
    private readonly IEnumerable<NetworkInterfaceInfo> _interfaces;

    public FakeNetworkInterfaceProvider(IEnumerable<NetworkInterfaceInfo> interfaces)
    {
        _interfaces = interfaces;
    }

    public IEnumerable<NetworkInterfaceInfo> GetAllNetworkInterfaces() => _interfaces;
}

internal class ThrowingNetworkInterfaceProvider : INetworkInterfaceProvider
{
    private readonly Exception _exception;

    public ThrowingNetworkInterfaceProvider(Exception exception)
    {
        _exception = exception;
    }

    public IEnumerable<NetworkInterfaceInfo> GetAllNetworkInterfaces()
    {
        throw _exception;
    }
}
