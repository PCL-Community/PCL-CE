using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Platform;

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

        var service = new TestFatalDialogService((_, _) =>
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

    [TestMethod]
    public void TruncateForInlineDisplayKeepsHeadAndTail()
    {
        var message = $"{new string('a', 5000)}MIDDLE{new string('b', 5000)}";

        var truncated = FatalDialogServiceBase.TruncateForInlineDisplay(message);

        Assert.IsLessThanOrEqualTo(
            8192,
            truncated.Length,
            $"截断后长度应不超过 8192，实际 {truncated.Length}");
        Assert.IsTrue(truncated.StartsWith(new string('a', 100), StringComparison.Ordinal));
        Assert.IsTrue(truncated.EndsWith(new string('b', 100), StringComparison.Ordinal));
        Assert.Contains("…", truncated);
    }

    private sealed class TestFatalDialogService(
        Func<string, string, bool> showNative) : FatalDialogServiceBase
    {
        protected override bool ShowFatalCore(string message, string caption)
        {
            return showNative(message, caption);
        }
    }
}