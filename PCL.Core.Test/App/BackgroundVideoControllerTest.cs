using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Media;
using PCL.Core.UI.Background;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class BackgroundVideoControllerTest
{
    [TestMethod]
    public void NormalizesFullRangeRgbForLimitedYuvOutput()
    {
        var info = new VideoInfo(
            new VideoSize(320, 240),
            30d,
            "rgb24",
            "pc",
            null);

        var normalized = BackgroundVideoController.NormalizeVideoInfoForOutput(info);

        Assert.AreEqual("rgb24", normalized?.PixelFormat);
        Assert.AreEqual("tv", normalized?.ColorRange);
        Assert.AreEqual("bt601", normalized?.ColorSpace);
    }

    [TestMethod]
    public void UsesBgraForUnsupportedColorSpaces()
    {
        var info = new VideoInfo(
            new VideoSize(3840, 2160),
            24d,
            "yuv420p",
            "tv",
            "bt2020c");

        Assert.IsTrue(
            BackgroundVideoController.RequiresBgraOutput(
                info with { ColorSpace = "smpte240m" }));
        Assert.IsFalse(
            BackgroundVideoController.RequiresBgraOutput(
                info with { ColorSpace = "bt709" }));
    }

    [TestMethod]
    public void LeavesYuvSourceColorInfoUnchanged()
    {
        var info = new VideoInfo(
            new VideoSize(320, 240),
            30d,
            "yuv420p",
            "pc",
            "bt709");

        var normalized = BackgroundVideoController.NormalizeVideoInfoForOutput(info);

        Assert.AreEqual("pc", normalized?.ColorRange);
        Assert.AreEqual("bt709", normalized?.ColorSpace);
    }
}