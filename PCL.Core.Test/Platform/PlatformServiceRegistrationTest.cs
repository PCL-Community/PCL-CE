using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;
using PCL.Platform;

namespace PCL.Core.Test.Platform;

[TestClass]
public sealed class PlatformServiceRegistrationTest
{
    [TestMethod]
    public void LinuxPlatformServicesResolveFromContainer()
    {
        using var provider = new ServiceCollection()
            .AddPlatformServices(PlatformKind.Linux)
            .BuildServiceProvider();

        Assert.IsNotNull(provider.GetRequiredService<IPlatformPathService>());
        Assert.IsNotNull(provider.GetRequiredService<ILaunchScriptService>());
        Assert.IsNotNull(provider.GetRequiredService<IServerLaunchScriptService>());
        Assert.IsNotNull(provider.GetRequiredService<IShellCommandService>());
        Assert.IsNotNull(provider.GetRequiredService<IProcessEnvironmentService>());
        Assert.IsNotNull(provider.GetRequiredService<IMinecraftPlatformRules>());
        Assert.IsNotNull(provider.GetRequiredService<IJavaPlatformInfo>());
        Assert.IsNotNull(provider.GetRequiredService<IRecycleBinService>());
        Assert.IsNotNull(provider.GetRequiredService<IRuntimeEnvironmentService>());
        Assert.IsNotNull(provider.GetRequiredService<IExternalCommandRunner>());
        Assert.IsNotNull(provider.GetRequiredService<IElevationService>());
        Assert.IsNotNull(provider.GetRequiredService<IProcessLauncher>());
    }

    [TestMethod]
    public void DetectedPlatformServicesResolveFromContainer()
    {
        var kind = PlatformDetector.Detect();
        using var provider = new ServiceCollection()
            .AddPlatformServices(kind)
            .BuildServiceProvider();

        Assert.IsNotNull(provider.GetRequiredService<IPlatformPathService>());
        Assert.IsNotNull(provider.GetRequiredService<ILaunchScriptService>());
        Assert.IsNotNull(provider.GetRequiredService<IServerLaunchScriptService>());
        Assert.IsNotNull(provider.GetRequiredService<IShellCommandService>());
        Assert.IsNotNull(provider.GetRequiredService<IProcessEnvironmentService>());
        Assert.IsNotNull(provider.GetRequiredService<IMinecraftPlatformRules>());
        Assert.IsNotNull(provider.GetRequiredService<IJavaPlatformInfo>());
        Assert.IsNotNull(provider.GetRequiredService<IRecycleBinService>());
        Assert.IsNotNull(provider.GetRequiredService<IRuntimeEnvironmentService>());
        Assert.IsNotNull(provider.GetRequiredService<IExternalCommandRunner>());
        Assert.IsNotNull(provider.GetRequiredService<IElevationService>());
        Assert.IsNotNull(provider.GetRequiredService<IProcessLauncher>());
    }

    [TestMethod]
    public void LinuxMinecraftRulesUseLinuxClassifiers()
    {
        var rules = new LinuxMinecraftPlatformRules();

        Assert.AreSequenceEqual(
            [
                "natives-linux",
                "linux-x86_64"
            ],
            [.. rules.GetNativeClassifierNames(Architecture.X64)]);
    }

    [TestMethod]
    public void OnlyWindowsGameWindowUsesPhysicalPixels()
    {
        Assert.IsTrue(new WindowsMinecraftPlatformRules().GameWindowUsesPhysicalPixels);
        Assert.IsFalse(new LinuxMinecraftPlatformRules().GameWindowUsesPhysicalPixels);
        Assert.IsFalse(new MacOsMinecraftPlatformRules().GameWindowUsesPhysicalPixels);
    }
}