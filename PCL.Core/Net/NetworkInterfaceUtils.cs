using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PCL.Core.Net;

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
        foreach (var iface in GetAvailableInterface())
        {
            var ipv6Addresses = iface.GetIPProperties().UnicastAddresses
                .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(addr => addr.Address)
                .ToArray();

            if (ipv6Addresses.Length == 0)
            {
                return IPv6Status.Unavailable;
            }

            foreach (var ip in ipv6Addresses)
            {
                if (_IsPublicIPv6(ip))
                {
                    return IPv6Status.Public;
                }
                if (_IsUniqueLocalIPv6(ip))
                {
                    return IPv6Status.RFC4193;
                }
            }

        }

        return IPv6Status.Unknown;
    }

    private static bool _IsVirtualInterface(NetworkInterface iface)
    {
        // 常见的虚拟接口类型和名称关键词
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
        byte[] addressBytes = ip.GetAddressBytes();
        // 公网IPv6地址范围：2000::/3（即首字节在0x20到0x3F之间）
        return addressBytes[0] >= 0x20 && addressBytes[0] <= 0x3F;
    }

    private static bool _IsUniqueLocalIPv6(IPAddress ip)
    {
        byte[] addressBytes = ip.GetAddressBytes();
        // 唯一本地地址范围：FC00::/7（即首字节为0xFC或0xFD）
        return addressBytes[0] == 0xFC || addressBytes[0] == 0xFD;
    }
}