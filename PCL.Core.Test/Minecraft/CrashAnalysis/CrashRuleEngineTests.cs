using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL.Core.Test.Minecraft.CrashAnalysis;

[TestClass]
public sealed class CrashRuleEngineTests
{
    [TestMethod]
    public void HighPriorityRuleFindsOutOfMemory()
    {
        var logs = new PreparedCrashLogs
        {
            GameText = new CrashTextSection("java.lang.OutOfMemoryError: Java heap space")
        };
        var findings = CrashRuleEngine.Analyze(logs, new CrashAnalysisRequest());
        Assert.Contains(finding => finding.Reason == CrashReasonCode.OutOfMemory, findings);
    }

    [TestMethod]
    public void StackTraceRuleRunsAfterPatternRulesMiss()
    {
        var logs = new PreparedCrashLogs
        {
            GameText = new CrashTextSection(
                "fabric-loader\n/FATAL] java.lang.RuntimeException\n\tat com.examplemod.SomeClass.method(SomeClass.java:1)\n[main/INFO] done")
        };
        var findings = CrashRuleEngine.Analyze(logs, new CrashAnalysisRequest());
        Assert.Contains(
            finding => finding.Reason is CrashReasonCode.StackTraceKeyword or CrashReasonCode.StackTraceModName,
            findings);
    }
}