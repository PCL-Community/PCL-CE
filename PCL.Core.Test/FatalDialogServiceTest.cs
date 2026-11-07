using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI;

namespace PCL.Core.Test;

[TestClass]
public sealed class FatalDialogServiceTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void ShowFatalSecondCallerWaitsForTheSameDialog()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var callCount = 0;

        var service = new FatalDialogService((_, _) =>
        {
            Interlocked.Increment(ref callCount);
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5));
            return true;
        });

        var first = new Thread(() => service.ShowFatal("first", "fatal"));
        var second = new Thread(() => service.ShowFatal("second", "fatal"));

        first.Start();
        Assert.IsTrue(
            entered.Wait(TimeSpan.FromSeconds(2), TestContext.CancellationToken),
            "第一个调用没有进入原生对话框");

        second.Start();
        Thread.Sleep(150);

        Assert.AreEqual(1, Volatile.Read(ref callCount), "重复的致命错误不应创建第二个对话框");
        Assert.IsTrue(second.IsAlive, "第二个调用应等待第一个对话框关闭");

        release.Set();

        Assert.IsTrue(first.Join(TimeSpan.FromSeconds(2)), "第一个调用未返回");
        Assert.IsTrue(second.Join(TimeSpan.FromSeconds(2)), "第二个调用未返回");
        Assert.AreEqual(1, Volatile.Read(ref callCount));
    }
}