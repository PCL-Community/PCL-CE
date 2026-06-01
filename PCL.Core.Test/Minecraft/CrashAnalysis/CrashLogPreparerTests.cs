using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL.Core.Test.Minecraft.CrashAnalysis;

[TestClass]
public sealed class CrashLogPreparerTests
{
    [TestMethod]
    public void ClassifyLegacyLauncherLogNames()
    {
        var file = new CrashLogFile { DisplayName = "PCL 启动器日志.txt", Content = "x" };
        Assert.AreEqual(CrashLogKind.LauncherLog, CrashLogPreparer.Classify(file));
    }

    [TestMethod]
    public void NewestCrashReportIsSelectedWithoutSortedListKeyConflict()
    {
        var now = DateTimeOffset.Now;
        var logs = new[]
        {
            new CrashLogFile { DisplayName = "crash-a.txt", Content = "a", LastWriteTime = now },
            new CrashLogFile { DisplayName = "crash-b.txt", Content = "b", LastWriteTime = now }
        };
        var prepared = new CrashLogPreparer().Prepare(logs, new CrashAnalysisRequest());
        Assert.IsNotNull(prepared.CrashReport);
    }
}