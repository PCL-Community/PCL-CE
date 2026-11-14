using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Platform;
using PCL.Core.Utils;

namespace PCL.Core.Test;

[TestClass]
public sealed class EasyTierInstallerTest
{
    [TestMethod]
    public void DownloadUrlsUsePlatformPackageAndFallbacks()
    {
        var package = EasyTierPlatform.Resolve(PlatformKind.Linux, Architecture.X64);
        var urls = EasyTierInstaller.GetDownloadUrls("2.6.4", package);

        Assert.HasCount(3, urls);
        Assert.AreEqual(
            "https://staticassets.naids.com/resources/pclce/static/easytier/easytier-linux-x86_64-v2.6.4.zip",
            urls[0]);
        Assert.AreEqual(
            "https://s3.pysio.online/pcl2-ce/static/easytier/easytier-linux-x86_64-v2.6.4.zip",
            urls[1]);
        Assert.AreEqual(
            "https://github.com/EasyTier/EasyTier/releases/download/v2.6.4/easytier-linux-x86_64-v2.6.4.zip",
            urls[2]);
    }

    [TestMethod]
    public void CliStartInfoUsesCurrentPlatformPaths()
    {
        var info = EasyTierCli.CreateStartInfo("-o json stun");

        Assert.AreEqual(EasyTierMetadata.CliPath, info.FileName);
        Assert.AreEqual(EasyTierMetadata.InstallRoot, info.WorkingDirectory);
        Assert.IsFalse(info.UseShellExecute);
        Assert.IsTrue(info.RedirectStandardOutput);
        Assert.IsTrue(info.RedirectStandardError);
        Assert.IsTrue(info.RedirectStandardInput);
    }

    [TestMethod]
    public void ParseStunInfoFromJson()
    {
        const string json = """
                            {
                              "udp_nat_type": 2,
                              "tcp_nat_type": 4,
                              "public_ip": ["1.2.3.4", "2001:db8::1"]
                            }
                            """;

        var stun = JsonSerializer.Deserialize<StunInfo>(json, JsonCompat.SerializerOptions);
        var status = CliNetTest.ParseNetStatus(stun);

        Assert.IsNotNull(status);
        Assert.AreEqual(CliNetTest.NatType.FullCone, status.UdpNatType);
        Assert.AreEqual(CliNetTest.NatType.PortRestricted, status.TcpNatType);
        Assert.IsTrue(status.SupportIPv6);
    }

    [TestMethod]
    public void ParseEmptyStunInfoReturnsNull()
    {
        Assert.IsNull(CliNetTest.ParseNetStatus(null));

        var stun = new StunInfo
        {
            UdpNatType = 0,
            TcpNatType = 0,
            Ips = ["1.2.3.4"]
        };
        var status = CliNetTest.ParseNetStatus(stun);

        Assert.IsNotNull(status);
        Assert.AreEqual(CliNetTest.NatType.OpenInternet, status.UdpNatType);
        Assert.IsFalse(status.SupportIPv6);
    }

    [TestMethod]
    public void RequiredFilesMatchPlatform()
    {
        var windows = EasyTierPlatform.Resolve(PlatformKind.Windows, Architecture.X64);
        var linux = EasyTierPlatform.Resolve(PlatformKind.Linux, Architecture.X64);

        Assert.Contains("Packet.dll", windows.RequiredFiles);
        Assert.DoesNotContain("Packet.dll", linux.RequiredFiles);
        Assert.HasCount(2, linux.RequiredFiles);
    }
}