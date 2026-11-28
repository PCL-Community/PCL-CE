using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.UI.Theme;
using YamlDotNet.Serialization;

namespace PCL.Core.Test;

[TestClass]
public class ThemeCustomColorTest
{
    [TestMethod]
    public void LegacyValuesShouldKeepPreviousMeaning()
    {
        var custom = ThemeCustomColor.FromLegacy(180, 80, 20, 90);

        Assert.AreEqual(180, custom.Hue);
        Assert.AreEqual(-0.2d, custom.ChromaScale, 0.0001d);
        Assert.AreEqual(0d, custom.LightnessBias, 0.0001d);
        Assert.AreEqual(0, custom.HueSpread);
    }

    [TestMethod]
    public void NormalizeShouldClampAllFields()
    {
        var custom = new ThemeCustomColor
        {
            Hue = -30,
            ChromaScale = 2d,
            LightnessBias = -2d,
            HueSpread = 180
        }.Normalize();

        Assert.AreEqual(330, custom.Hue);
        Assert.AreEqual(1d, custom.ChromaScale);
        Assert.AreEqual(-1d, custom.LightnessBias);
        Assert.AreEqual(90, custom.HueSpread);
    }

    [TestMethod]
    public void SrgbRoundTripShouldStayCloseToPickedColor()
    {
        foreach (
            var tone in new[]
            {
                ToneProfileConfig.DefaultLight,
                ToneProfileConfig.DefaultDark
            })
        foreach (
            var spread in new[]
            {
                0,
                45,
                -45
            })
        {
            var custom = ThemeCustomColor.FromSrgb(71, 133, 202, tone, spread, 200);
            var generated = custom.ToReferenceColor(tone).ScRgb;
            var expected = OklabColor.FromRgb(71, 133, 202).ScRgb;

            Assert.AreEqual(expected.Red, generated.Red, 0.002f);
            Assert.AreEqual(expected.Green, generated.Green, 0.002f);
            Assert.AreEqual(expected.Blue, generated.Blue, 0.002f);
        }
    }

    [TestMethod]
    public void ReferenceColorShouldIgnoreHueSpread()
    {
        var withoutSpread = new ThemeCustomColor
        {
            Hue = 210,
            ChromaScale = -0.2d,
            HueSpread = 0
        };
        var withSpread = withoutSpread with
        {
            HueSpread = 45
        };

        var expected = withoutSpread.ToReferenceColor(ToneProfileConfig.DefaultLight).ScRgb;
        var actual = withSpread.ToReferenceColor(ToneProfileConfig.DefaultLight).ScRgb;

        Assert.AreEqual(expected.Red, actual.Red, 0.0001f);
        Assert.AreEqual(expected.Green, actual.Green, 0.0001f);
        Assert.AreEqual(expected.Blue, actual.Blue, 0.0001f);
    }

    [TestMethod]
    public void TitleBarGradientShouldUseSpreadOnlyForEndpoints()
    {
        var custom = new ThemeCustomColor
        {
            Hue = 210,
            ChromaScale = -0.2d,
            HueSpread = 30
        };

        var (start, middle, end) = custom.ToTitleBarColors(ToneProfileConfig.DefaultLight);

        Assert.AreEqual(180d, start.Hue, 0.5d);
        Assert.AreEqual(210d, middle.Hue, 0.5d);
        Assert.AreEqual(240d, end.Hue, 0.5d);
    }

    [TestMethod]
    public void PaletteShouldIgnoreHueSpread()
    {
        var withoutSpread = ThemeService.CalculateColorResources(
            ToneProfileConfig.DefaultLight,
            (210, 0, -0.2, 0));
        var withSpread = ThemeService.CalculateColorResources(
            ToneProfileConfig.DefaultLight,
            (210, 0, -0.2, 45));

        for (var i = 0; i < withoutSpread.Length; i++)
        {
            Assert.AreEqual(withoutSpread[i].Red, withSpread[i].Red, 0.0001f);
            Assert.AreEqual(withoutSpread[i].Green, withSpread[i].Green, 0.0001f);
            Assert.AreEqual(withoutSpread[i].Blue, withSpread[i].Blue, 0.0001f);
        }
    }

    [TestMethod]
    public void GrayscaleShouldUseFallbackHue()
    {
        var custom = ThemeCustomColor.FromSrgb(
            128,
            128,
            128,
            ToneProfileConfig.DefaultLight,
            0,
            210);

        Assert.AreEqual(210, custom.Hue);
    }

    [TestMethod]
    public void YamlRoundTripShouldKeepValues()
    {
        var custom = new ThemeCustomColor
        {
            Hue = 210,
            ChromaScale = 0.15d,
            LightnessBias = -0.2d,
            HueSpread = 30
        };
        var serializer = new SerializerBuilder().DisableAliases().Build();
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithEnforceRequiredMembers()
            .Build();

        var yaml = serializer.Serialize(custom);
        var result = deserializer.Deserialize<ThemeCustomColor>(yaml);

        Assert.AreEqual(custom, result);
    }

    [TestMethod]
    public void ColorResourcesShouldContainExpectedSuffixes()
    {
        var resources = ThemeService.CalculateColorResources(
            ToneProfileConfig.DefaultLight,
            (255, 0, -0.2, 0));
        var suffixes = resources.Select(resource => resource.Suffix).ToArray();

        Assert.AreSequenceEqual(
            new[]
            {
                "1",
                "2",
                "3",
                "4",
                "5",
                "6",
                "7",
                "8",
                "SemiTransparent",
                "Bg0",
                "Bg1"
            },
            suffixes,
            SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void PresetArgumentsShouldStayUnchanged()
    {
        AssertArgs((235, 0.36, 0.2, 0), ColorTheme.SkyBlue);
        AssertArgs((255, 0, -0.2, 0), ColorTheme.CatBlue);
        AssertArgs((268, -0.05, -0.1, 0), ColorTheme.DeathBlue);
        AssertArgs((275, -0.03, -0.35, 0), ColorTheme.HmclBlue);
    }

    private static void AssertArgs(
        (int Hue, double LightAdjust, double ChromaAdjust, int HueSpread) expected,
        ColorTheme theme)
    {
        var actual = ThemeService.GetThemeArgs(theme, false);
        Assert.AreEqual(expected.Hue, actual.Hue);
        Assert.AreEqual(expected.LightAdjust, actual.LightAdjust, 0.0001d);
        Assert.AreEqual(expected.ChromaAdjust, actual.ChromaAdjust, 0.0001d);
        Assert.AreEqual(expected.HueSpread, actual.HueSpread);
    }
}