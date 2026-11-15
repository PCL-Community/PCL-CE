using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;

namespace PCL.Core.Test.Platform;

[TestClass]
public sealed class PlatformDetectorTest
{
    [TestMethod]
    public void DetectReturnsCurrentOperatingSystem()
    {
        if (!OperatingSystem.IsWindows()
            && !OperatingSystem.IsLinux()
            && !OperatingSystem.IsMacOS())
        {
            Assert.ThrowsExactly<PlatformNotSupportedException>(() => PlatformDetector.Detect());
            return;
        }

        var platform = PlatformDetector.Detect();

        if (OperatingSystem.IsWindows())
            Assert.AreEqual(PlatformKind.Windows, platform);
        else if (OperatingSystem.IsLinux())
            Assert.AreEqual(PlatformKind.Linux, platform);
        else
            Assert.AreEqual(PlatformKind.MacOs, platform);
    }
}