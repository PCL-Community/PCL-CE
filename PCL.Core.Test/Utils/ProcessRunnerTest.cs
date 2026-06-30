using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.OS;

namespace PCL.Core.Test.Utils;

[TestClass]
public class ProcessRunnerTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void CapturesProcessOutput()
    {
        var result = ProcessRunner
            .CaptureAsync(
                "cmd.exe",
                "/c echo hello",
                5000,
                cancellationToken: TestContext.CancellationToken)
            .GetAwaiter()
            .GetResult();

        Assert.IsFalse(result.TimedOut);
        Assert.AreEqual(0, result.ExitCode);
        Assert.Contains("hello", result.CombinedOutput);
    }
}