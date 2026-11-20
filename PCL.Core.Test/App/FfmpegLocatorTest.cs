using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;
using PCL.Media;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class FfmpegLocatorTest
{
    [TestMethod]
    public async Task DetectsConfiguredPathWithSpaces()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"pcl ffmpeg {Guid.NewGuid():N}");
        await File.WriteAllTextAsync(path, string.Empty);
        try
        {
            var runner = new RecordingRunner();
            var locator = new FfmpegLocator(runner);

            var result = await locator.DetectAsync(path, true, CancellationToken.None);

            Assert.IsTrue(result.IsAvailable);
            Assert.AreEqual(path, result.ExecutablePath);
            Assert.AreEqual(path, runner.LastFileName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ReportsInvalidConfiguredPathWithoutRunningIt()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"pcl missing ffmpeg {Guid.NewGuid():N}");
        var runner = new RecordingRunner();
        var locator = new FfmpegLocator(runner);

        var result = await locator.DetectAsync(path, true, CancellationToken.None);

        Assert.AreEqual(FfmpegAvailability.Invalid, result.Availability);
        Assert.IsNull(runner.LastFileName);
    }

    private sealed class RecordingRunner : IExternalCommandRunner
    {
        public string? LastFileName { get; private set; }

        public bool ExistsOnPath(string command)
        {
            return false;
        }

        public Task<ExternalCommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            LastFileName = fileName;
            return Task.FromResult(
                new ExternalCommandResult(0, "ffmpeg version test", string.Empty));
        }

        public IExternalCommandSession Start(
            string fileName,
            IReadOnlyList<string> arguments)
        {
            throw new NotSupportedException();
        }

        public IDisposable Watch(
            string fileName,
            IReadOnlyList<string> arguments,
            Action<string> onOutput,
            Action? onExit = null)
        {
            throw new NotSupportedException();
        }
    }
}