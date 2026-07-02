using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Test.Utils;

[TestClass]
public class TextUtilsTest
{
    [TestMethod]
    public void LeftPadOrTrimKeepsLegacyBehavior()
    {
        Assert.AreEqual("00abc", TextUtils.LeftPadOrTrim("abc", "0", 5));
        Assert.AreEqual("abc", TextUtils.LeftPadOrTrim("abcdef", "0", 3));
    }

    [TestMethod]
    public void EscapesLikePattern()
    {
        Assert.AreEqual("a[*][?][#][[]b[]]", TextUtils.EscapeLikePattern("a*?#[b]"));
    }

    [TestMethod]
    public void EscapesXamlAttributeTextWithMarkupExtensionPrefix()
    {
        Assert.AreEqual("{}{Binding Name}", TextUtils.EscapeXamlAttributeText("{Binding Name}"));
        Assert.AreEqual("a&amp;b&quot;c", TextUtils.EscapeXamlAttributeText("a&b\"c"));
    }

    [TestMethod]
    public void StringSliceExtensionsKeepUnmatchedText()
    {
        Assert.AreEqual("2026", "2026/06/30".BeforeFirst("/"));
        Assert.AreEqual("06/30", "2026/06/30".AfterFirst("/"));
        Assert.AreEqual("30", "2026/06/30".Between("/", "/"));
        Assert.AreEqual("2026/06/30", "2026/06/30".BeforeFirst("#"));
    }
}