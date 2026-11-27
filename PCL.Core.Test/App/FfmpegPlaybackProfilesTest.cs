using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Media;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class FfmpegPlaybackProfilesTest
{
    [TestMethod]
    public void HardwareProfileComesFirstWhenEnabled()
    {
        var profiles = FfmpegPlaybackProfiles.Create(
            true,
            CreateInfo(1920, 1080, 30d),
            16,
            FfmpegPixelFormat.Yuv420p);

        Assert.AreEqual("hardware", profiles[0].Id);
        Assert.IsTrue(profiles[0].Options.HardwareAcceleration);
        Assert.AreEqual(6, profiles[0].FirstFrameTimeout.TotalSeconds);
    }

    [TestMethod]
    public void DisabledHardwareStartsWithSoftwareProfile()
    {
        var profiles = FfmpegPlaybackProfiles.Create(
            false,
            CreateInfo(1920, 1080, 30d),
            16,
            FfmpegPixelFormat.Yuv420p);

        Assert.IsFalse(profiles.Any(profile => profile.Options.HardwareAcceleration));
        Assert.AreEqual("software-low-delay", profiles[0].Id);
    }

    [TestMethod]
    public void LimitsThreadsForHighResolutionAndSlowSource()
    {
        var profiles = FfmpegPlaybackProfiles.Create(
            false,
            CreateInfo(3840, 2160, 24d),
            16,
            FfmpegPixelFormat.Yuv420p);

        var software = profiles.Single(profile => profile.Id == "software");
        Assert.AreEqual(4, software.Options.DecoderThreads);
        Assert.AreEqual(1, software.Options.FilterThreads);
        Assert.IsNull(software.Options.FpsCap);
    }

    [TestMethod]
    public void CapsFpsForHighFrameRateSource()
    {
        var profiles = FfmpegPlaybackProfiles.Create(
            false,
            CreateInfo(1920, 1080, 60d),
            8,
            FfmpegPixelFormat.Bgra8888);

        var software = profiles.Single(profile => profile.Id == "software");
        Assert.AreEqual(2, software.Options.DecoderThreads);
        Assert.AreEqual(30d, software.Options.FpsCap);
    }

    [TestMethod]
    public void UsesUnknownSourceFallback()
    {
        var profiles = FfmpegPlaybackProfiles.Create(
            false,
            null,
            8,
            FfmpegPixelFormat.Bgra8888);

        var software = profiles.Single(profile => profile.Id == "software");
        Assert.AreEqual(2, software.Options.DecoderThreads);
        Assert.AreEqual(30d, software.Options.FpsCap);
    }

    private static VideoInfo CreateInfo(int width, int height, double frameRate)
    {
        return new VideoInfo(
            new VideoSize(width, height),
            frameRate,
            "yuv420p",
            "tv",
            "bt709");
    }
}