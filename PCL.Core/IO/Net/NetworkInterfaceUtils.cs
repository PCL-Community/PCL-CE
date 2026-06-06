using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PCL.Core.IO.Net;

public static class NetworkInterfaceUtils
{
    public static List<NetworkInterface> GetAvailableInterface()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(iface => !_IsVirtualInterface(iface))
            .ToList();
    }

    public enum IPv6Status
    {
        Unknown,
        Public,
        RFC4193,
        Unavailable,
    }

    public static IPv6Status GetIPv6Status()
    {
        var hasPublic = false;
        var hasRfc4193 = false;
        var hasAny = false;

        foreach (var iface in GetAvailableInterface())
        {
            foreach (var addr in iface.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetworkV6) continue;

                hasAny = true;
                if (_IsLinkLocalIPv6(addr.Address))
                    continue; // 跳过链路本地地址 FE80::/10

                if (_IsPublicIPv6(addr.Address))
                    hasPublic = true;
                else if (_IsUniqueLocalIPv6(addr.Address))
                    hasRfc4193 = true;
            }
        }

        if (hasPublic) return IPv6Status.Public;
        if (hasRfc4193) return IPv6Status.RFC4193;
        return hasAny ? IPv6Status.Unknown : IPv6Status.Unavailable;
    }

    private static bool _IsVirtualInterface(NetworkInterface iface)
    {
        var virtualTypes = new[] {
            NetworkInterfaceType.Loopback,
            NetworkInterfaceType.Tunnel,
            NetworkInterfaceType.Ppp
        };

        var virtualKeywords = new[] {
            "virtual",
            "pseudo",
            "loopback",
            "tunnel",
            "vpn",
            "ppp",
            "veth",
            "docker",
            "hyper-v",
            "vmware",
            "virtualbox"
        };

        return virtualTypes.Contains(iface.NetworkInterfaceType) ||
               virtualKeywords.Any(keyword => iface.Description.ToLower().Contains(keyword));
    }

    private static bool _IsPublicIPv6(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return bytes[0] >= 0x20 && bytes[0] <= 0x3F;
    }

    private static bool _IsUniqueLocalIPv6(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return bytes[0] == 0xFC || bytes[0] == 0xFD;
    }

    private static bool _IsLinkLocalIPv6(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return bytes.Length >= 2 && bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80;
    }
}
