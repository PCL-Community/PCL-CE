using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils;

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
            stunInfo = await JsonSerializer.DeserializeAsync<StunInfo>(reader, JsonCompat.SerializerOptions);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Link", "Failed to do net test");
        }
        if (stunInfo is null) return null;

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

    public static string GetNatTypeString(NatType type)
    {
        return Lang.Text(type switch
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
}