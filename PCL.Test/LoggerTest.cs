using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Helper.Logger;

namespace PCL.Test;

[TestClass]
public class LoggerTest
{
    [TestMethod]
    public void TestSimpleWrite()
    {
        var loggerOps = new LoggerConfiguration(
            Path.Combine(Path.GetTempPath(), "PCLTest", "Logger"),
            LoggerSegmentMode.BySize,
            10 * 1024 * 1024,
            null,
            true,
            10);
        var logger = new Logger(loggerOps);
        for (var i = 0; i < 10; i++)
            logger.Info($"Current we got {i}");
    }

    [TestMethod]
    public async Task TestHeavyWrite()
    {
        var loggerOps = new LoggerConfiguration(
            Path.Combine(Path.GetTempPath(), "PCLTest", "Logger"),
            LoggerSegmentMode.BySize,
            5 * 1024 * 1024,
            null,
            true,
            10);
        var logger = new Logger(loggerOps);
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
            }));
        }
        await Task.WhenAll(tasks.ToArray());
    }
}