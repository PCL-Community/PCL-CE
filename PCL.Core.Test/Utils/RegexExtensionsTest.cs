using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Test.Utils;

[TestClass]
public class RegexExtensionsTest
{
    private static readonly string[] Expected = ["12", "34"];

    [TestMethod]
    public void FindsAndChecksMatches()
    {
        Assert.AreSequenceEqual(Expected, "a12b34".RegexSearch(@"\d+"));
        Assert.AreEqual("12", "a12b34".RegexSeek(@"\d+"));
        Assert.IsTrue("a12".RegexCheck(@"\d+"));
    }

    [TestMethod]
    public void ReplacesEachMatch()
    {
        var replaced = "a12b34".RegexReplaceEach(@"\d+", match => $"[{match.Value}]");
        Assert.AreEqual("a[12]b[34]", replaced);
    }
}