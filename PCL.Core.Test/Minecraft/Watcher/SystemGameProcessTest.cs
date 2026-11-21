using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Watcher;

namespace PCL.Core.Test.Minecraft.Watcher;

[TestClass]
public sealed class SystemGameProcessTest
{
    [TestMethod]
    public void HasExitedReturnsFalseWhenProcessCannotBeQueried()
    {
        using var process = new Process();
        using var wrapper = new SystemGameProcess(process);

        Assert.IsFalse(wrapper.HasExited);
        Assert.IsFalse(wrapper.WaitForExit(TimeSpan.Zero));
    }
}