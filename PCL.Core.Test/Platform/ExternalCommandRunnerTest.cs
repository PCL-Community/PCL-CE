using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Platform;

namespace PCL.Core.Test.Platform;

[TestClass]
public sealed class ExternalCommandRunnerTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RunAsyncCapturesStandardOutput()
    {
        var runner = new ExternalCommandRunner();
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[]
            {
                "/c",
                "echo",
                "pcl-command-test"
            })
            : ("sh", new[]
            {
                "-c",
                "printf pcl-command-test"
            });

        var result = await runner.RunAsync(
            fileName,
            arguments,
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        Assert.IsTrue(result.Succeeded);
        Assert.Contains("pcl-command-test", result.StandardOutput);
    }

    [TestMethod]
    public async Task StartCapturesStandardOutput()
    {
        var runner = new ExternalCommandRunner();
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[]
            {
                "/c",
                "echo",
                "pcl-stream-test"
            })
            : ("sh", new[]
            {
                "-c",
                "printf pcl-stream-test"
            });

        using var session = runner.Start(fileName, arguments);
        using var reader = new StreamReader(session.StandardOutput);
        var output = await reader.ReadToEndAsync(TestContext.CancellationToken);
        await session.WaitForExitAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, session.ExitCode);
        Assert.Contains("pcl-stream-test", output);
    }

    [TestMethod]
    public void ExistsOnPathFindsSystemShell()
    {
        var runner = new ExternalCommandRunner();
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "sh";

        Assert.IsTrue(runner.ExistsOnPath(command));
        Assert.IsFalse(runner.ExistsOnPath("pcl-command-that-does-not-exist"));
    }
}