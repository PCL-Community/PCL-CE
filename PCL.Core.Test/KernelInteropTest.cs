using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.OS;

namespace PCL.Core.Test;

[TestClass]
public class KernelInteropTest
{
    [TestMethod]
    public void PhysicalMemoryShouldBeAvailableOnCurrentPlatform()
    {
        var (total, available) = KernelInterop.GetPhysicalMemoryBytes();
        if (!OperatingSystem.IsWindows()
            && !OperatingSystem.IsLinux()
            && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("当前平台未实现物理内存查询");

        Assert.IsTrue(total > 0UL, "总物理内存应大于 0");
        Assert.IsTrue(available <= total, "可用物理内存不应大于总物理内存");
        Assert.IsTrue(KernelInterop.GetAvailablePhysicalMemoryBytes() <= total);
        var load = KernelInterop.GetMemoryLoadPercent();
        Assert.IsTrue(load is >= 0d and <= 100d, $"内存占用百分比越界：{load}");
    }
}
