using System;
using System.Globalization;
using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI.Animation;

namespace PCL.Core.Test.Animation;

[TestClass]
public sealed class NColorTest
{
    [TestMethod]
    public void FromHsl2HandlesHueWrapping()
    {
        for (var hue = 0d; hue < 360d; hue += 15d)
        {
            var color = NColor.FromHsl2(hue, 90d, 55d);
            Assert.IsGreaterThan(0d, color.A);
            Assert.IsTrue(color.R >= 0d && color.R <= 255d);
            Assert.IsTrue(color.G >= 0d && color.G <= 255d);
            Assert.IsTrue(color.B >= 0d && color.B <= 255d);
        }
    }

    [TestMethod]
    public void AlphaReturnsIndependentCopy()
    {
        var color = new NColor(10d, 20d, 30d, 40d);
        var transparent = color.Alpha(12d);

        Assert.AreEqual(12d, transparent.A, 1e-6d);
        Assert.AreEqual(40d, color.A, 1e-6d);
    }

    [TestMethod]
    public void ColorConversionClampsChannels()
    {
        var color = new NColor(300d, -10d, 128d, 999d);
        var converted = color.ToColor();

        Assert.AreEqual(255, converted.R);
        Assert.AreEqual(0, converted.G);
        Assert.AreEqual(128, converted.B);
        Assert.AreEqual(255, converted.A);
    }

    [TestMethod]
    public void ParsesAllHexFormats()
    {
        AssertColor("#f00", 255, 0, 0, 255);
        AssertColor("#f008", 255, 0, 0, 136);
        AssertColor("#ff8000", 255, 128, 0, 255);
        AssertColor("#ff800080", 255, 128, 0, 128);
        AssertColor("  #FF800080  ", 255, 128, 0, 128);
    }

    [TestMethod]
    public void TryParseRejectsInvalidInput()
    {
        Assert.IsFalse(NColor.TryParse(null, CultureInfo.InvariantCulture, out _));
        Assert.IsFalse(NColor.TryParse("", CultureInfo.InvariantCulture, out _));
        Assert.IsFalse(NColor.TryParse("ff8000", CultureInfo.InvariantCulture, out _));
        Assert.IsFalse(NColor.TryParse("#12", CultureInfo.InvariantCulture, out _));
        Assert.IsFalse(NColor.TryParse("#gggggg", CultureInfo.InvariantCulture, out _));
        Assert.IsFalse(NColor.TryParse("#1234567", CultureInfo.InvariantCulture, out _));
    }

    [TestMethod]
    public void GenericParseUsesIParsable()
    {
        var color = ParseValue<NColor>("#11223344");
        var spanColor = ParseSpanValue<NColor>("#11223344");

        Assert.AreEqual(0x11, color.ToColor().R);
        Assert.AreEqual(0x22, color.ToColor().G);
        Assert.AreEqual(0x33, color.ToColor().B);
        Assert.AreEqual(0x44, color.ToColor().A);
        Assert.AreEqual(color.ToColor(), spanColor.ToColor());
    }

    [TestMethod]
    public void ToStringRoundTrips()
    {
        var color = new NColor(1, 2, 3, 4);
        var text = color.ToString();
        var parsed = NColor.Parse(text, CultureInfo.InvariantCulture);

        Assert.AreEqual("#01020304", text);
        Assert.AreEqual(color.ToColor(), parsed.ToColor());
    }

    [TestMethod]
    public void ColorRoundTripsAllChannels()
    {
        for (var i = 0; i < 256; i++)
        {
            var color = Color.FromArgb(
                (byte)i,
                (byte)(255 - i),
                (byte)i,
                (byte)((i * 37) & 0xFF));
            Assert.AreEqual(color, new NColor(color).ToColor());
        }
    }

    [TestMethod]
    public void ScRgbConversionMatchesKnownValues()
    {
        var color = new NColor(128, 64, 32, 255);
        var (alpha, red, green, blue) = color.ToScRgb();

        Assert.AreEqual(1f, alpha, 1e-6f);
        Assert.AreEqual(0.2158605f, red, 1e-5f);
        Assert.AreEqual(0.0512695f, green, 1e-5f);
        Assert.AreEqual(0.0144438f, blue, 1e-5f);

        var roundTrip = NColor.FromScRgb(alpha, red, green, blue);
        Assert.AreEqual(color.ToColor(), roundTrip.ToColor());
    }

    [TestMethod]
    public void ScRgbSupportsExtendedRange()
    {
        var color = NColor.FromScRgb(0.5d, 1.5d, -0.25d, 0.25d);

        Assert.AreEqual(0.5f, color.ScA, 1e-6f);
        Assert.AreEqual(1.5f, color.ScR, 1e-6f);
        Assert.AreEqual(-0.25f, color.ScG, 1e-6f);
        Assert.AreEqual(0.25f, color.ScB, 1e-6f);

        var converted = color.ToColor();
        Assert.AreEqual(255, converted.R);
        Assert.AreEqual(0, converted.G);
        Assert.AreEqual(128, converted.A);
    }

    [TestMethod]
    public void FromArgbUsesAlphaFirstOrder()
    {
        var color = NColor.FromArgb(50d, 255d, 255d, 255d).ToColor();

        Assert.AreEqual(255, color.R);
        Assert.AreEqual(255, color.G);
        Assert.AreEqual(255, color.B);
        Assert.AreEqual(50, color.A);
    }

    [TestMethod]
    public void NullSolidColorBrushIsTransparent()
    {
        SolidColorBrush? brush = null;
        NColor color = brush;

        Assert.AreEqual(0, color.ToColor().A);
    }

    [TestMethod]
    public void ArithmeticKeepsSrgbSemantics()
    {
        var midpoint = (new NColor(255, 255, 255) - new NColor(0, 0, 0)) * 0.5f;

        Assert.AreEqual(127, midpoint.ToColor().R);
        Assert.AreEqual(127, midpoint.ToColor().G);
        Assert.AreEqual(127, midpoint.ToColor().B);
    }

    private static void AssertColor(
        string text,
        byte r,
        byte g,
        byte b,
        byte a)
    {
        var color = NColor.Parse(text, CultureInfo.InvariantCulture).ToColor();
        Assert.AreEqual(r, color.R);
        Assert.AreEqual(g, color.G);
        Assert.AreEqual(b, color.B);
        Assert.AreEqual(a, color.A);
    }

    private static T ParseValue<T>(string text) where T : IParsable<T>
    {
        return T.Parse(text, CultureInfo.InvariantCulture);
    }

    private static T ParseSpanValue<T>(string text) where T : ISpanParsable<T>
    {
        return T.Parse(text.AsSpan(), CultureInfo.InvariantCulture);
    }
}