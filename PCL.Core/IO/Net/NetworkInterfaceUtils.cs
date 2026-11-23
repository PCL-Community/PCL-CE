using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PCL.Core.IO.Net;

public static class NetworkInterfaceUtils
{
    public enum IPv6Status
    {
        Unknown,
        Public,
        RFC4193,
        Unavailable
    }

    public static List<NetworkInterface> GetAvailableInterface()
    {
        return
        [
            .. NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(i => !_IsVirtualInterface(i))
        ];
    }

    public static IPv6Status GetIPv6Status()
    {
        var hasEligibleInterface = false;
        var hasIPv6 = false;
        var hasUniqueLocal = false;

        foreach (var i in GetAvailableInterface())
        {
            hasEligibleInterface = true;

            var ipv6Addresses = i
                .GetIPProperties()
                .UnicastAddresses
                .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(addr => addr.Address)
                .ToArray();

            if (ipv6Addresses.Length == 0) continue;

            hasIPv6 = true;

            foreach (var ip in ipv6Addresses)
            {
                if (_IsPublicIPv6(ip)) return IPv6Status.Public;
                if (_IsUniqueLocalIPv6(ip)) hasUniqueLocal = true;
            }
        }

        if (hasUniqueLocal)
            return IPv6Status.RFC4193;

        if (hasIPv6)
            return IPv6Status.Unknown;

        return hasEligibleInterface
            ? IPv6Status.Unavailable
            : IPv6Status.Unknown;
    }

    private static bool _IsVirtualInterface(NetworkInterface i)
    {
        // 常见的虚拟接口类型和名称关键词
        var virtualTypes = new[]
        {
            NetworkInterfaceType.Loopback,
            NetworkInterfaceType.Tunnel,
            NetworkInterfaceType.Ppp
        };

        var virtualKeywords = new[]
        {
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

        return Enumerable.Contains(virtualTypes, i.NetworkInterfaceType)
            || virtualKeywords.Any(keyword => i.Description.Contains(
                keyword,
                StringComparison.InvariantCultureIgnoreCase));
    }

    private static bool _IsPublicIPv6(IPAddress ip)
    {
        var addressBytes = ip.GetAddressBytes();
        // 公网IPv6地址范围：2000::/3（即首字节在0x20到0x3F之间）
        return addressBytes[0] >= 0x20 && addressBytes[0] <= 0x3F;
    }

    private static bool _IsUniqueLocalIPv6(IPAddress ip)
    {
        var addressBytes = ip.GetAddressBytes();
        // 唯一本地地址范围：FC00::/7（即首字节为0xFC或0xFD）
        return addressBytes[0] == 0xFC || addressBytes[0] == 0xFD;
    }
}