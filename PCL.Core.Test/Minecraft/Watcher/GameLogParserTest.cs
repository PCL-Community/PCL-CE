using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Watcher;

namespace PCL.Core.Test.Minecraft.Watcher;

[TestClass]
public sealed class GameLogParserTest
{
    [DataTestMethod]
    [DataRow("[12:00:00] [main/DEBUG]: debug", GameLogLevel.Debug)]
    [DataRow("[12:00:00] [main/INFO]: info", GameLogLevel.Info)]
    [DataRow("[12:00:00] [Render thread/WARN]: warn", GameLogLevel.Warn)]
    [DataRow("[12:00:00] [main/ERROR]: error", GameLogLevel.Error)]
    [DataRow("[12:00:00] [main/FATAL]: fatal", GameLogLevel.Fatal)]
    [DataRow("plain line", GameLogLevel.Info)]
    [DataRow("Exception in thread \"main\" java.lang.RuntimeException", GameLogLevel.Error)]
    public void ClassifyLevel_ReturnsExpectedLevel(string line, GameLogLevel expected)
    {
        Assert.AreEqual(expected, GameLogParser.ClassifyLevel(line, GameLogLevel.Info));
    }

    [TestMethod]
    public void ClassifyLevel_KeepsWarnForStackFrames()
    {
        Assert.AreEqual(
            GameLogLevel.Warn,
            GameLogParser.ClassifyLevel("\tat example.Foo.bar(Foo.java:1)", GameLogLevel.Warn));
        Assert.AreEqual(
            GameLogLevel.Warn,
            GameLogParser.ClassifyLevel("Caused by: java.lang.Exception", GameLogLevel.Warn));
    }

    [TestMethod]
    public void ClassifyLevel_DoesNotKeepInfoForStackFrames()
    {
        Assert.AreEqual(
            GameLogLevel.Info,
            GameLogParser.ClassifyLevel("\tat example.Foo.bar(Foo.java:1)", GameLogLevel.Info));
    }

    [DataTestMethod]
    [DataRow("\tat example.Foo.bar(Foo.java:1)", true)]
    [DataRow("Caused by: java.lang.Exception", true)]
    [DataRow("\t... 3 more", true)]
    [DataRow("normal line", false)]
    public void IsContinuationLine_DetectsStackFrames(string line, bool expected)
    {
        Assert.AreEqual(expected, GameLogParser.IsContinuationLine(line));
    }

    [TestMethod]
    public void DetectProgress_AdvancesThroughKnownStages()
    {
        var stage = GameLogParser.DetectProgress("hello", GameLogStage.None);
        Assert.AreEqual(GameLogStage.Output, stage);

        stage = GameLogParser.DetectProgress("Setting user: player", stage);
        Assert.AreEqual(GameLogStage.User, stage);

        stage = GameLogParser.DetectProgress("LWJGL Version: 3.3.3", stage);
        Assert.AreEqual(GameLogStage.Lwjgl, stage);

        stage = GameLogParser.DetectProgress("OpenAL initialized.", stage);
        Assert.AreEqual(GameLogStage.Sound, stage);

        stage = GameLogParser.DetectProgress("Created: 1024x1024 textures-atlas", stage);
        Assert.AreEqual(GameLogStage.Textures, stage);
    }

    [TestMethod]
    public void DetectProgress_DoesNotRegress()
    {
        var stage = GameLogParser.DetectProgress(
            "Created: 1024x1024 textures-atlas",
            GameLogStage.Textures);
        Assert.AreEqual(GameLogStage.Textures, stage);

        stage = GameLogParser.DetectProgress("hello", stage);
        Assert.AreEqual(GameLogStage.Textures, stage);
    }

    [DataTestMethod]
    [DataRow("Restarting Minecraft with command foo", 1)]
    [DataRow("Someone is closing me!", 1)]
    [DataRow("Crash report saved to: crash.txt", 2)]
    [DataRow("This crash report has been saved to: crash.txt", 2)]
    [DataRow("Could not save crash report to crash.txt", 2)]
    [DataRow("[12:00:00] [main/ERROR]: Unable to launch", 2)]
    [DataRow("[CHAT] Crash report saved to: fake", 0)]
    [DataRow("ordinary line", 0)]
    public void DetectSignal_ClassifiesMarkers(string line, int expected)
    {
        Assert.AreEqual((GameLogSignal)expected, GameLogParser.DetectSignal(line));
    }
}