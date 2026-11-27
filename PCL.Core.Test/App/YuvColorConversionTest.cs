using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI.Media;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class YuvColorConversionTest
{
    [TestMethod]
    public void DefaultsUnknownColorSpaceToBt601()
    {
        var limited = YuvColorConversion.Create("tv", null);
        var full = YuvColorConversion.Create("pc", null);

        Assert.AreEqual(1.59602678f, limited.RowR[2], 0.000001f);
        Assert.AreEqual(1.402f, full.RowR[2], 0.000001f);
    }

    [TestMethod]
    public void KeepsExplicitBt709()
    {
        var conversion = YuvColorConversion.Create("tv", "bt709");

        Assert.AreEqual(1.79274107f, conversion.RowR[2], 0.000001f);
    }
}