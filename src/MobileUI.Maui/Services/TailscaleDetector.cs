using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace StrategyTradingAppUI.Maui.Services;

public interface INetworkInterfaceProvider
{
    IEnumerable<NetworkInterfaceInfo> GetAllNetworkInterfaces();
}

public class NetworkInterfaceInfo
{
    public required OperationalStatus Status { get; init; }
    public required IList<IPAddress> IPv4Addresses { get; init; }
}

internal class DefaultNetworkInterfaceProvider : INetworkInterfaceProvider
{
    public IEnumerable<NetworkInterfaceInfo> GetAllNetworkInterfaces()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Select(nic => new NetworkInterfaceInfo
            {
                Status = nic.OperationalStatus,
                IPv4Addresses = nic.GetIPProperties()
                    .UnicastAddresses
                    .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(addr => addr.Address)
                    .ToList()
            });
    }
}

public class TailscaleDetectorService
{
    private readonly INetworkInterfaceProvider _provider;

    public TailscaleDetectorService(INetworkInterfaceProvider? provider = null)
    {
        _provider = provider ?? new DefaultNetworkInterfaceProvider();
    }

    public bool IsConnected()
    {
        try
        {
            foreach (var nic in _provider.GetAllNetworkInterfaces())
            {
                if (nic.Status != OperationalStatus.Up)
                    continue;

                foreach (var address in nic.IPv4Addresses)
                {
                    var bytes = address.GetAddressBytes();
                    if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                        return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}

public static class TailscaleDetector
{
    private static readonly TailscaleDetectorService _service = new();

    public static bool IsConnected() => _service.IsConnected();
}
