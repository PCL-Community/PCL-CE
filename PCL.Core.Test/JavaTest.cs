using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Scanner;
using PCL.Core.Platform;
using PCL.Platform;

namespace PCL.Core.Test;

[TestClass]
public class JavaTest
{
    [TestMethod]
    public async Task TestJavaSearch()
    {
        // Java 搜索是否稳定
        await using var provider = new ServiceCollection()
            .AddPlatformServices(PlatformDetector.Detect())
            .BuildServiceProvider();
        var javaInfo = provider.GetRequiredService<IJavaPlatformInfo>();
        var jas = new JavaManager(
            javaInfo.CreateParser(),
            new RegistryJavaScanner(),
            new DefaultPathsScanner(),
            new PathEnvironmentScanner(),
            new MicrosoftStoreJavaScanner(),
            new WindowsWhereCommandScanner());
        await jas.ScanJavaAsync();
        var firstScanned = jas.GetSortedJavaList();
        foreach (var ja in firstScanned)
        {
            Console.WriteLine(ja.ToString());
            Assert.IsGreaterThan(
                0,
                ja.Installation.Version.Major,
                "Java version is not valid: " + ja.Installation.JavaFolder);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ja.Installation.JavaFolder));
        }

        await jas.ScanJavaAsync();
        var secondScanned = jas.GetSortedJavaList();
        Assert.HasCount(secondScanned.Count, firstScanned);
        // Java 搜索是否能够正确选择
        Assert.IsTrue(
            secondScanned.Count == 0
            || (secondScanned.Count > 0
                && (await jas.SelectSuitableJavaAsync(
                    new Version(1, 8, 0),
                    new Version(30, 0, 0)))
                .Length
                > 0));
        // Java 是否有重复
        Assert.DoesNotContain(
            x => x.Count() > 1,
            secondScanned.GroupBy(x => x.Installation.JavaExePath));
    }
}