using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PCL.Core.Utils.OS;

public static class NetworkUtils
{
    private static readonly IPAddress[] _LocalIpAddresses = NetworkInterface.GetAllNetworkInterfaces()
        .Where(x => x is { OperationalStatus: OperationalStatus.Up })
        .SelectMany(x => x.GetIPProperties().UnicastAddresses)
        .Where(ua => ua.Address is { AddressFamily: AddressFamily.InterNetwork or AddressFamily.InterNetworkV6 } &&
                     !ua.Address.Equals(IPAddress.Any) &&
                     !ua.Address.Equals(IPAddress.IPv6Any) &&
                     !ua.Address.Equals(IPAddress.Loopback) &&
                     !ua.Address.Equals(IPAddress.IPv6Loopback))
        .Select(ua => ua.Address)
        .ToArray();

    public static IPAddress[] GetAllLocalAddress() => _LocalIpAddresses;
}