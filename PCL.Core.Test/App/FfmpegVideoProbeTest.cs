using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Media;
using PCL.Core.Platform;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class FfmpegVideoProbeTest
{
    private const string Output =
        "Stream #0:0[0x1](und): Video: h264 (High) (avc1 / 0x31637661), yuv420p(progressive), 320x180 [SAR 1:1 DAR 16:9], 42 kb/s";

    [TestMethod]
    public void ParsesVideoSize()
    {
        var size = FfmpegVideoProbe.Parse(Output);

        Assert.AreEqual(320, size?.Width);
        Assert.AreEqual(180, size?.Height);
    }

    [TestMethod]
    public void ParsesFrameRateAndColorInfo()
    {
        var info = FfmpegVideoProbe.ParseInfo(
            "Stream #0:0: Video: h264, yuv420p(tv, bt709, progressive), 1920x1080, 24 fps, 24 tbr, 15360 tbn");

        Assert.AreEqual(1920, info?.Size.Width);
        Assert.AreEqual(1080, info?.Size.Height);
        Assert.AreEqual(24d, info?.FrameRate);
        Assert.AreEqual("yuv420p", info?.PixelFormat);
        Assert.AreEqual("tv", info?.ColorRange);
        Assert.AreEqual("bt709", info?.ColorSpace);
    }

    [TestMethod]
    public void ParsesFullRangeAndBt601ColorInfo()
    {
        var info = FfmpegVideoProbe.ParseInfo(
            "Stream #0:0: Video: h264, yuv420p(pc, smpte170m), 720x480, 29.97 fps");

        Assert.AreEqual(29.97d, info?.FrameRate);
        Assert.AreEqual("pc", info?.ColorRange);
        Assert.AreEqual("smpte170m", info?.ColorSpace);
    }

    [TestMethod]
    public void NormalizesBt2020NonConstantLuminanceColorSpace()
    {
        var info = FfmpegVideoProbe.ParseInfo(
            "Stream #0:0: Video: h264, yuv420p(tv, bt2020nc, progressive), 3840x2160, 24 fps");

        Assert.AreEqual("tv", info?.ColorRange);
        Assert.AreEqual("bt2020", info?.ColorSpace);
    }

    [TestMethod]
    public void PreservesBt2020ConstantLuminanceColorSpace()
    {
        var info = FfmpegVideoProbe.ParseInfo(
            "Stream #0:0: Video: h264, yuv420p(tv, bt2020c, progressive), 3840x2160, 24 fps");

        Assert.AreEqual("tv", info?.ColorRange);
        Assert.AreEqual("bt2020c", info?.ColorSpace);
    }

    [TestMethod]
    public void PreservesUnsupportedYuvColorSpace()
    {
        var info = FfmpegVideoProbe.ParseInfo(
            "Stream #0:0: Video: h264, yuv420p(tv, smpte240m, progressive), 1920x1080, 24 fps");

        Assert.AreEqual("tv", info?.ColorRange);
        Assert.AreEqual("smpte240m", info?.ColorSpace);
    }

    [TestMethod]
    public void UsesSampleAspectRatioForDisplaySize()
    {
        var size = FfmpegVideoProbe.Parse(
            "Stream #0:0: Video: h264, yuv420p, 320x240 [SAR 2:1 DAR 8:3], 34 kb/s");

        Assert.AreEqual(640, size?.Width);
        Assert.AreEqual(240, size?.Height);
    }

    [TestMethod]
    public void SwapsDimensionsForQuarterTurnRotation()
    {
        var size = FfmpegVideoProbe.Parse(
            "Stream #0:0: Video: h264, yuv420p, 320x240 [SAR 1:1 DAR 4:3]\n"
            + "  Side data:\n"
            + "    Display Matrix: rotation of 90.00 degrees");

        Assert.AreEqual(240, size?.Width);
        Assert.AreEqual(320, size?.Height);
    }

    [TestMethod]
    public async Task ProbesFirstVideoStream()
    {
        var runner = new RecordingRunner();
        var probe = new FfmpegVideoProbe(runner);

        var size = await probe.ProbeAsync(
            "ffmpeg",
            "video.mp4",
            CancellationToken.None);

        Assert.AreEqual(320, size?.Width);
        Assert.AreEqual(180, size?.Height);
        Assert.AreEqual("ffmpeg", runner.LastFileName);
        Assert.Contains("file,pipe", runner.LastArguments);
    }

    private sealed class RecordingRunner : IExternalCommandRunner
    {
        public string? LastFileName { get; private set; }

        public IReadOnlyList<string> LastArguments { get; private set; } = [];

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
            LastArguments = arguments;
            return Task.FromResult(new ExternalCommandResult(1, string.Empty, Output));
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