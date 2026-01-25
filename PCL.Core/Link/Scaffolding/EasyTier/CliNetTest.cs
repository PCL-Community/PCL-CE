using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.Link.Scaffolding.EasyTier;

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
    public record NetStatus
    {
        public required NatType UdpNatType;
        public required NatType TcpNatType;
        public required bool SupportIPv6;
    }

    public async static Task<NetStatus?> GetNetStatusAsync()
    {
        using var cliProcess = new Process();
        cliProcess.StartInfo = new ProcessStartInfo
        {
            FileName = $"{EasyTierMetadata.EasyTierFilePath}\\easytier-cli.exe",
            WorkingDirectory = EasyTierMetadata.EasyTierFilePath,
            Arguments = $"-o json stun",
            ErrorDialog = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };
        cliProcess.EnableRaisingEvents = true;
        cliProcess.Start();
        var reader = PipeReader.Create(cliProcess.StandardOutput.BaseStream);

        StunInfo? stunInfo = null;
        try
        {
            stunInfo = await JsonSerializer.DeserializeAsync<StunInfo>(reader);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Link", "Failed to do net test");
        }
        if (stunInfo == null) return null;

        var supportIPv6 = false;
        foreach (var ip in stunInfo.Ips)
        {
            if (ip.Contains(":"))
            {
                supportIPv6 = true;
                break;
            }
        }

        return new NetStatus { UdpNatType = GetNatTypeViaCode(stunInfo.UdpNatType), TcpNatType = GetNatTypeViaCode(stunInfo.TcpNatType), SupportIPv6 = supportIPv6 };
    }

    public static NatType GetNatTypeViaCode(int type) => type switch
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
    
    public static string GetNatTypeString(NatType type) => type switch
    {
        NatType.OpenInternet => "开放",
        NatType.NoPat => "开放",
        NatType.FullCone => "中等（完全圆锥）",
        NatType.PortRestricted => "中等（端口受限圆锥）",
        NatType.Restricted => "中等（受限圆锥）",
        NatType.SymmetricEasy => "严格（宽松对称）",
        NatType.Symmetric => "严格（对称）",
        NatType.SymmetricFirewall => "严格（对称防火墙）",
        NatType.UdpBlocked => "严格（阻止 UDP）",
        _ => "未知"
    };
}