using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL.Core.Test.Minecraft.CrashAnalysis;

[TestClass]
public sealed class CrashTextTests
{
    [TestMethod]
    public void NormalizeNewLines_Mixed()
    {
        Assert.AreEqual("a\nb\nc\nd", CrashText.NormalizeNewLines("a\r\nb\nc\rd"));
    }

    [TestMethod]
    public void HeadTailDistinct_RemovesDuplicateLines()
    {
        var result = CrashText.HeadTail(CrashText.ReadLines("a\nb\na\nc\nd\ne"), 2, 2);
        Assert.AreEqual("a\nb\nd\ne", result);
    }
}