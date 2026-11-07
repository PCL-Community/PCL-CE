using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Scaffolding.EasyTier;

namespace PCL.Core.Test;

[TestClass]
public sealed class EasyTierPlatformTest
{
    [TestMethod]
    public void ResolveWindowsPackages()
    {
        var x64 = EasyTierPlatform.Resolve(EasyTierHostOs.Windows, Architecture.X64);
        Assert.AreEqual("easytier-windows-x86_64", x64.FolderName);
        Assert.AreEqual("easytier-core.exe", x64.CoreFileName);
        Assert.AreEqual("easytier-cli.exe", x64.CliFileName);
        Assert.Contains("Packet.dll", x64.RequiredFiles);
        Assert.AreEqual("easytier-windows-x86_64-v2.6.4.zip", x64.GetArchiveName("2.6.4"));

        var arm64 = EasyTierPlatform.Resolve(EasyTierHostOs.Windows, Architecture.Arm64);
        Assert.AreEqual("easytier-windows-arm64", arm64.FolderName);

        var x86 = EasyTierPlatform.Resolve(EasyTierHostOs.Windows, Architecture.X86);
        Assert.AreEqual("easytier-windows-i686", x86.FolderName);
    }

    [TestMethod]
    public void ResolveLinuxPackages()
    {
        var x64 = EasyTierPlatform.Resolve(EasyTierHostOs.Linux, Architecture.X64);
        Assert.AreEqual("easytier-linux-x86_64", x64.FolderName);
        Assert.AreEqual("easytier-core", x64.CoreFileName);
        Assert.AreEqual("easytier-cli", x64.CliFileName);
        Assert.DoesNotContain("Packet.dll", x64.RequiredFiles);

        var arm64 = EasyTierPlatform.Resolve(EasyTierHostOs.Linux, Architecture.Arm64);
        Assert.AreEqual("easytier-linux-aarch64", arm64.FolderName);
    }

    [TestMethod]
    public void ResolveMacPackages()
    {
        var x64 = EasyTierPlatform.Resolve(EasyTierHostOs.MacOs, Architecture.X64);
        Assert.AreEqual("easytier-macos-x86_64", x64.FolderName);
        Assert.AreEqual("easytier-core", x64.CoreFileName);
        Assert.AreEqual("easytier-cli", x64.CliFileName);

        var arm64 = EasyTierPlatform.Resolve(EasyTierHostOs.MacOs, Architecture.Arm64);
        Assert.AreEqual("easytier-macos-aarch64", arm64.FolderName);
    }

    [TestMethod]
    public void UnsupportedArchitectureThrows()
    {
        Assert.ThrowsExactly<PlatformNotSupportedException>(() =>
            EasyTierPlatform.Resolve(EasyTierHostOs.Linux, Architecture.S390x));
        Assert.ThrowsExactly<PlatformNotSupportedException>(() =>
            EasyTierPlatform.Resolve(EasyTierHostOs.MacOs, Architecture.X86));
    }
}