using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI.Theming;

namespace PCL.Core.Test;

[TestClass]
public class ThemeColorTextTest
{
    [TestMethod]
    public void HexShouldNormalizeShortAndLongForms()
    {
        Assert.IsTrue(ThemeColorText.TryParseHex("#abc", out var shortColor));
        Assert.AreEqual("#AABBCC", $"#{ThemeColorText.FormatHex(shortColor)}");

        Assert.IsTrue(ThemeColorText.TryParseHex("4785ca", out var longColor));
        Assert.AreEqual("#4785CA", $"#{ThemeColorText.FormatHex(longColor)}");

        Assert.IsFalse(ThemeColorText.TryParseHex("GGGGGG", out _));
        Assert.IsFalse(ThemeColorText.TryParseHex("#12", out _));
    }

    [TestMethod]
    public void NonFiniteNumbersShouldBeRejected()
    {
        Assert.IsFalse(ThemeColorText.TryParseNumber("NaN", out _));
        Assert.IsFalse(ThemeColorText.TryParseNumber("Infinity", out _));
        Assert.IsFalse(ThemeColorText.TryParseNumber("-Infinity", out _));
        Assert.IsFalse(ThemeColorText.TryParseNumber("1e309", out _));
    }

    [TestMethod]
    public void NonFiniteFieldsShouldBeRejected()
    {
        Assert.IsFalse(ThemeColorText.TryParseHsvFields("NaN", "50", "50", out _, out _));
        Assert.IsFalse(ThemeColorText.TryParseRgbFields("NaN", "50", "50", out _));
    }

    [TestMethod]
    public void HsvShouldNormalizeHueAndClampComponents()
    {
        Assert.IsTrue(ThemeColorText.TryParseHsvFields("400", "150", "-5", out _, out var hue));
        Assert.AreEqual(40d, hue, 0.0001d);

        var color = ThemeColorText.CreateHsv(400d, 150d, 50d);
        var (formattedHue, saturation, value) = ThemeColorText.FormatHsv(color);
        Assert.AreEqual(40, formattedHue);
        Assert.AreEqual(100, saturation);
        Assert.AreEqual(50, value);
    }

    [TestMethod]
    public void RgbShouldClampAndRoundComponents()
    {
        var color = ThemeColorText.CreateRgb(300d, -10d, 127.6d);
        var (red, green, blue) = ThemeColorText.FormatRgb(color);

        Assert.AreEqual((byte)255, red);
        Assert.AreEqual((byte)0, green);
        Assert.AreEqual((byte)128, blue);
    }
}