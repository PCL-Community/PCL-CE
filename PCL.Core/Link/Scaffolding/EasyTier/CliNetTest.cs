using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Localization;
using PCL.Core.Logging;

namespace PCL.Core.Link.Scaffolding.EasyTier;

/// <summary>
///     通过 EasyTier CLI 的 <c>stun</c> 子命令检测 NAT 类型。
///     该功能在 Windows、Linux 与 macOS 上使用同一套 CLI，不按操作系统禁用。
/// </summary>
public class CliNetTest
{
    public enum NatType
    {
        Unknown,
        OpenInternet,
        NoPat,
        FullCone,
        Restricted,
        PortRestricted,
        SymmetricEasy,
        Symmetric,
        SymmetricFirewall,
        UdpBlocked
    }

    /// <summary>
    ///     执行 NAT 检测；CLI 缺失、超时或输出无法解析时返回 <c>null</c>。
    /// </summary>
    public static async Task<NetStatus?> GetNetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stunInfo = await EasyTierCli
                .RunJsonAsync<StunInfo>(
                    "-o json stun",
                    TimeSpan.FromSeconds(20),
                    cancellationToken)
                .ConfigureAwait(false);

            return ParseNetStatus(stunInfo);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Link", "Failed to do net test");
            return null;
        }
    }

    internal static NetStatus? ParseNetStatus(StunInfo? stunInfo)
    {
        if (stunInfo is null)
            return null;

        var supportIPv6 = false;
        if (stunInfo.Ips is not { } ips)
            return new NetStatus
            {
                UdpNatType = GetNatTypeViaCode(stunInfo.UdpNatType),
                TcpNatType = GetNatTypeViaCode(stunInfo.TcpNatType),
                SupportIPv6 = supportIPv6
            };

        supportIPv6 = ips.Any(ip => ip.Contains(':'));
        return new NetStatus
        {
            UdpNatType = GetNatTypeViaCode(stunInfo.UdpNatType),
            TcpNatType = GetNatTypeViaCode(stunInfo.TcpNatType),
            SupportIPv6 = supportIPv6
        };
    }

    public static NatType GetNatTypeViaCode(int type)
    {
        return type switch
        {
            0 => NatType.OpenInternet,
            1 => NatType.NoPat,
            2 => NatType.FullCone,
            3 => NatType.Restricted,
            4 => NatType.PortRestricted,
            5 => NatType.SymmetricEasy,
            6 => NatType.Symmetric,
            7 => NatType.SymmetricFirewall,
            8 => NatType.UdpBlocked,
            _ => NatType.Unknown
        };
    }

    public static string GetNatTypeString(NatType type)
    {
        return Lang.Text(
            type switch
            {
                NatType.OpenInternet or NatType.NoPat => "Link.Nat.Type.Open",
                NatType.FullCone => "Link.Nat.Type.FullCone",
                NatType.PortRestricted => "Link.Nat.Type.PortRestricted",
                NatType.Restricted => "Link.Nat.Type.Restricted",
                NatType.SymmetricEasy => "Link.Nat.Type.SymmetricEasy",
                NatType.Symmetric => "Link.Nat.Type.Symmetric",
                NatType.SymmetricFirewall => "Link.Nat.Type.SymmetricFirewall",
                NatType.UdpBlocked => "Link.Nat.Type.UdpBlocked",
                _ => "Link.Nat.Type.Unknown"
            });
    }

    public record NetStatus
    {
        public required bool SupportIPv6;
        public required NatType TcpNatType;
        public required NatType UdpNatType;
    }
}