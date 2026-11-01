using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = PCL.Core.Logging.LogLevel;

namespace PCL.Core.Test;

[TestClass]
public class LoggerTest
{
    [TestMethod]
    public async Task TestSimpleWrite()
    {
        var folder = _CreateTempFolder();
        var logger = new Logger(new LoggerConfiguration(folder, MinLogLevel: LogLevel.Trace));

        for (var i = 0; i < 10; i++)
            logger.Info($"Current we got {i}");

        var files = logger.CurrentLogFiles;
        await logger.DisposeAsync();

        var lines = _ReadAll(files).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static x => x.TrimEnd('\r'))
            .ToArray();

        Assert.AreEqual(10, lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            StringAssert.Matches(
                lines[i],
                new Regex(@"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[INFO\] \[[^\]]+\] Current we got " + i + "$"));
        }
    }

    [TestMethod]
    public async Task TestHeavyWrite()
    {
        const int threadCount = 25;
        const int perThread = 25565;

        var folder = _CreateTempFolder();
        var logger = new Logger(new LoggerConfiguration(folder, MinLogLevel: LogLevel.Trace));

        var tasks = new List<Task>();
        for (var i = 0; i < threadCount; i++)
        {
            var current = i;
            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < perThread; j++)
                    logger.Info($"Current we got {current}:{j}");
            }, TestContext.CancellationToken));
        }

        await Task.WhenAll(tasks.ToArray());

        var files = logger.CurrentLogFiles;
        var dropped = logger.DroppedLogCount;
        await logger.DisposeAsync();

        var written = files.Sum(static f => File.Exists(f) ? File.ReadLines(f).Count() : 0);
        Assert.AreEqual((long)threadCount * perThread, written + dropped);
    }

    [TestMethod]
    public async Task TestMinLogLevelFiltersFile()
    {
        var folder = _CreateTempFolder();
        var logger = new Logger(new LoggerConfiguration(folder, MinLogLevel: LogLevel.Error));

        logger.Debug("debug-line");
        logger.Info("info-line");
        logger.Error("error-line");

        var files = logger.CurrentLogFiles;
        await logger.DisposeAsync();

        var content = _ReadAll(files);
        StringAssert.Contains(content, "error-line");
        Assert.IsFalse(content.Contains("debug-line"), "Debug entries must not reach the file.");
        Assert.IsFalse(content.Contains("info-line"), "Info entries must not reach the file.");
    }

    [TestMethod]
    public async Task TestDisposeIsIdempotent()
    {
        var folder = _CreateTempFolder();
        var logger = new Logger(new LoggerConfiguration(folder, MinLogLevel: LogLevel.Trace));

        logger.Info("before-dispose");
        await logger.DisposeAsync();
        await logger.DisposeAsync();
        logger.Info("after-dispose");
    }

    [TestMethod]
    public async Task TestAdapterScopeIsolation()
    {
        var folder = _CreateTempFolder();
        var logger = new Logger(new LoggerConfiguration(folder, MinLogLevel: LogLevel.Trace));
        ILogger adapter = new LoggerAdapter(logger, "TestCat");

        using (adapter.BeginScope("A"))
        {
            await Task.Run(() =>
            {
                using (adapter.BeginScope("B"))
                    adapter.LogInformation("child-line");
            }, TestContext.CancellationToken);

            adapter.LogInformation("parent-line");
        }

        var files = logger.CurrentLogFiles;
        await logger.DisposeAsync();

        var content = _ReadAll(files);
        StringAssert.Contains(content, "[TestCat] [A => B] child-line");
        StringAssert.Contains(content, "[TestCat] [A] parent-line");
        Assert.IsFalse(content.Contains("[TestCat] [A => B] parent-line"), "A child scope must not leak into the parent context.");
    }

    [TestMethod]
    public async Task TestAdapterExceptionOnce()
    {
        const string marker = "UNIQUE_MARKER_42";

        var folder = _CreateTempFolder();
        var logger = new Logger(new LoggerConfiguration(folder, MinLogLevel: LogLevel.Trace));
        ILogger adapter = new LoggerAdapter(logger, "TestCat");

        adapter.LogError(new InvalidOperationException(marker), "failed-line");

        var files = logger.CurrentLogFiles;
        await logger.DisposeAsync();

        var content = _ReadAll(files);
        Assert.AreEqual(1, _CountOccurrences(content, marker), "The exception text must be rendered exactly once.");
    }

    private static string _CreateTempFolder([CallerMemberName] string name = "")
    {
        var path = Path.Combine(Path.GetTempPath(), "PCLTest", "Logger", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string _ReadAll(IReadOnlyList<string> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files)
            if (File.Exists(file))
                builder.Append(File.ReadAllText(file));
        return builder.ToString();
    }

    private static int _CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    public TestContext TestContext { get; set; }
}
