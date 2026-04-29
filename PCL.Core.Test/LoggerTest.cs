using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Logging;

namespace PCL.Core.Test;

[TestClass]
public class LoggerTest
{
    [TestMethod]
    public void TestSimpleWrite()
    {
        var loggerOps = new LoggerConfiguration(
            Path.Combine(Path.GetTempPath(), "PCLTest", "Logger"));
        var logger = new Logger(loggerOps);
        for (var i = 0; i < 10; i++)
            logger.Info($"Current we got {i}");
    }

    [TestMethod]
    public async Task TestHeavyWrite()
    {
        var loggerOps = new LoggerConfiguration(
            Path.Combine(Path.GetTempPath(), "PCLTest", "Logger"));
        await using var logger = new Logger(loggerOps);
        var tasks = new List<Task>();
        for (var i = 0; i < 25; i++)
        {
            int current = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 25565; j++)
                {
                    logger.Info($"Current we got {current}:{j}");
                }
            }, TestContext.CancellationToken));
        }
        await Task.WhenAll(tasks.ToArray());
    }

    public TestContext TestContext { get; set; }
}