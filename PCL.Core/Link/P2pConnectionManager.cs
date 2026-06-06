using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Link.Natayark;

namespace PCL.Core.Link;

public enum P2pPhase
{
    NotStarted,
    Ipv6Direct,
    UpnpIpv4,
    Failed
}

public sealed class P2pConnectionResult
{
    public P2pPhase Phase { get; set; }
    public string? FormattedAddress { get; set; }
    public int Port { get; set; }
    public List<DiagnosticItem> Diagnostics { get; set; } = [];
}

public sealed class DiagnosticItem
{
    public string Phase { get; init; } = "";
    public string Symptom { get; init; } = "";
    public string Suggestion { get; init; } = "";
}

/// <summary>
///     P2P 联机连接管理器。
///     状态机：IPv6 直连 → UPnP IPv4 → 失败诊断。
/// </summary>
public sealed class P2pConnectionManager : IDisposable
{
    private readonly FirewallService _firewall = new();
    private readonly UpnpService _upnp = new();
    private bool _disposed;

    /// <summary>
    ///     尝试建立 P2P 连接入口。
    /// </summary>
    /// <param name="internalPort">本地 Minecraft 端口</param>
    /// <param name="externalPort">期望的公网端口</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>连接结果</returns>
    public async Task<P2pConnectionResult> ConnectAsync(int internalPort, int externalPort, CancellationToken ct = default)
    {
        var result = new P2pConnectionResult();

        // ---- 阶段 1: IPv6 直连 ----
        result.Phase = P2pPhase.Ipv6Direct;
        var ipv6 = Ipv6Utils.GetBestPublicIPv6();
        if (ipv6 is not null)
        {
            // 添加防火墙规则
            var ruleName = $"PCL_N_P2P_IPv6_{externalPort}";
            _firewall.AddInboundRule(externalPort, "TCP", ruleName);

            // IPv6 环境：直接监听即可，无需 NAT 穿透
            result.FormattedAddress = Ipv6Utils.FormatForMc(ipv6.ToString(), externalPort);
            result.Port = externalPort;
            result.Phase = P2pPhase.Ipv6Direct;
            return result;
        }

        result.Diagnostics.Add(new DiagnosticItem
        {
            Phase = "IPv6 直连",
            Symptom = "未检测到公网 IPv6 地址",
            Suggestion = "检查光猫和路由器是否开启 IPv6 功能；检查路由器 IPv6 防火墙是否拦截入站连接"
        });

        // ---- 阶段 2: UPnP IPv4 ----
        result.Phase = P2pPhase.UpnpIpv4;

        using var upnpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        upnpCts.CancelAfter(TimeSpan.FromSeconds(15));

        var localAddr = GetBestLocalIPv4();
        if (localAddr is not null)
        {
            var upnpResult = await _upnp.MapAsync(
                localAddr,
                internalPort,
                externalPort,
                "TCP",
                "PCL N Edition P2P",
                upnpCts.Token,
                discoverTimeout: 3000);

            if (upnpResult is not null)
            {
                result.FormattedAddress = $"{upnpResult.PublicIp}:{upnpResult.ExternalPort}";
                result.Port = upnpResult.ExternalPort;
                result.Phase = P2pPhase.UpnpIpv4;
                return result;
            }
        }

        result.Diagnostics.Add(new DiagnosticItem
        {
            Phase = "UPnP 映射",
            Symptom = "无法配置路由器端口映射",
            Suggestion =
                "登录路由器管理后台，确认 UPnP 已开启；\n" +
                "若路由器不支持 UPnP，请手动配置端口转发：\n" +
                $"将外部 TCP {externalPort} 端口转发至本机 {internalPort} 端口"
        });

        // ---- 阶段 3: 失败 ----
        result.Phase = P2pPhase.Failed;
        result.Diagnostics.Add(new DiagnosticItem
        {
            Phase = "替代方案",
            Symptom = "自动化方案均无法在当前网络环境下生效",
            Suggestion =
                "使用第三方虚拟局域网工具（如 ZeroTier、Radmin LAN、蒲公英）组建虚拟局域网，\n" +
                "然后使用 Minecraft 原生局域网联机（LAN）功能进行游戏。\n" +
                "详细教程请参考 PCL 帮助文档。"
        });

        return result;
    }

    private static IPAddress? GetBestLocalIPv4()
    {
        return Dns.GetHostEntry(Dns.GetHostName())
            .AddressList
            .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(a) &&
                        !a.ToString().StartsWith("169.254")) // 排除 APIPA
            .OrderByDescending(a => IsPrivateIPv4(a))         // 优先私有地址
            .FirstOrDefault();
    }

    private static bool IsPrivateIPv4(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false;

        // 10.x, 172.16-31.x, 192.168.x
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _upnp.Dispose();
        _firewall.Dispose();
    }
}
