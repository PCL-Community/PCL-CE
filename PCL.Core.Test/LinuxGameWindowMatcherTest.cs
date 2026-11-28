using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Platform;

namespace PCL.Core.Test;

[TestClass]
public sealed class LinuxGameWindowMatcherTest
{
    [TestMethod]
    public void VanillaMinecraftTitleReachesThreshold()
    {
        Assert.AreEqual(3, LinuxGameWindowMatcher.Score("", "Minecraft 1.21.5"));
        Assert.IsGreaterThanOrEqualTo(
            LinuxGameWindowMatcher.GameWindowThreshold,
            LinuxGameWindowMatcher.Score("", "Minecraft 1.21.5"));
    }

    [TestMethod]
    public void JavaApplicationIdAddsScore()
    {
        Assert.AreEqual(5, LinuxGameWindowMatcher.Score("java", "Minecraft 1.21.5"));
        Assert.AreEqual(3, LinuxGameWindowMatcher.Score("java", "Untitled"));
    }

    [TestMethod]
    public void LoaderTitlesReachThreshold()
    {
        Assert.AreEqual(3, LinuxGameWindowMatcher.Score("", "FML Early Loading Progress"));
        Assert.AreEqual(3, LinuxGameWindowMatcher.Score("", "Quilt Loader 0.27.0"));
    }

    [TestMethod]
    public void UnrelatedWindowsStayBelowThreshold()
    {
        Assert.AreEqual(1, LinuxGameWindowMatcher.Score("firefox", "Mozilla Firefox"));
        Assert.AreEqual(1, LinuxGameWindowMatcher.Score("pcl-ce", "PCL CE"));
        Assert.AreEqual(1, LinuxGameWindowMatcher.Score("", ""));
        Assert.AreEqual(1, LinuxGameWindowMatcher.Score(null, null));
    }

    [TestMethod]
    public void MatchingIsCaseInsensitive()
    {
        Assert.AreEqual(5, LinuxGameWindowMatcher.Score("JAVA", "minecraft"));
        Assert.AreEqual(3, LinuxGameWindowMatcher.Score("", "MINECRAFT"));
    }
}