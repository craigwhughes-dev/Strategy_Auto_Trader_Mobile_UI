using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace StrategyTradingAppUI.Maui.Services;

public static class TailscaleDetector
{
    public static bool IsConnected()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    var bytes = addr.Address.GetAddressBytes();
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
