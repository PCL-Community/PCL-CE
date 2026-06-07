using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PCL.Core.Link;

public static class Ipv6Utils
{
    /// <summary>
    ///     获取所有公网 IPv6 地址（2000::/3），排除链路本地和 ULA。
    /// </summary>
    public static IReadOnlyList<IPAddress> GetPublicIPv6Addresses()
    {
        var result = new List<IPAddress>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (IsVirtualInterface(ni)) continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetworkV6) continue;
                if (IsPublicIPv6(addr.Address))
                    result.Add(addr.Address);
            }
        }

        return result;
    }

    /// <summary>
    ///     是否属于公网 IPv6 范围（2000::/3）。
    /// </summary>
    public static bool IsPublicIPv6(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetworkV6) return false;
        if (ip.IsIPv6LinkLocal) return false;
        if (ip.IsIPv6SiteLocal) return false;
        var bytes = ip.GetAddressBytes();
        if (bytes.Length < 1) return false;
        var first = bytes[0];
        return first is >= 0x20 and <= 0x3F;
    }

    /// <summary>
    ///     是否属于链路本地 IPv6（FE80::/10）。
    /// </summary>
    public static bool IsLinkLocalIPv6(IPAddress ip)
    {
        return ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv6LinkLocal;
    }

    /// <summary>
    ///     是否属于唯一本地地址（FC00::/7 或 FEC0::/10）。
    /// </summary>
    public static bool IsUniqueLocalIPv6(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetworkV6) return false;
        var bytes = ip.GetAddressBytes();
        if (bytes.Length < 1) return false;
        // FC00::/7 (ULA) or FEC0::/10 (deprecated Site-Local)
        return (bytes[0] & 0xFE) == 0xFC;
    }

    /// <summary>
    ///     按 Minecraft 格式包裹 IPv6 地址：[ipv6]。
    /// </summary>
    public static string FormatForMc(string ip, int port)
    {
        return IPAddress.TryParse(ip, out var addr) && addr.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{ip}]:{port}"
            : $"{ip}:{port}";
    }

    /// <summary>
    ///     挑选最适合联机的 IPv6 地址（优先物理网卡 + 临时地址）。
    /// </summary>
    public static IPAddress? GetBestPublicIPv6()
    {
        var result = new List<(IPAddress addr, bool isTemporary, bool isPhysical)>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (IsVirtualInterface(ni)) continue;

            var isPhysical = ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                             ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetworkV6) continue;
                if (!IsPublicIPv6(addr.Address)) continue;
                result.Add((addr.Address,
                    addr.SuffixOrigin == SuffixOrigin.LinkLayerAddress || addr.SuffixOrigin == SuffixOrigin.Random,
                    isPhysical));
            }
        }

        return result
            .OrderByDescending(a => a.isPhysical)   // 优先物理网卡
            .ThenByDescending(a => a.isTemporary)    // 再优先临时地址
            .Select(a => a.addr)
            .FirstOrDefault();
    }

    private static bool IsVirtualInterface(NetworkInterface ni)
    {
        var desc = ni.Description?.ToLower() ?? "";
        var name = ni.Name?.ToLower() ?? "";

        return desc.Contains("virtual") || desc.Contains("hyper-v") || desc.Contains("vmware") ||
               desc.Contains("virtualbox") || desc.Contains("docker") || desc.Contains("vpn") ||
               desc.Contains("tunnel") || desc.Contains("loopback") || desc.Contains("pseudo") ||
               name.Contains("vethernet") || name.Contains("vswitch") || name.Contains("docker") ||
               ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
               ni.NetworkInterfaceType == NetworkInterfaceType.Loopback;
    }
}
