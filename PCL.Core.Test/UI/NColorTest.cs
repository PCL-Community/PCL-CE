using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI;

namespace PCL.Core.Test.UI;

[TestClass]
public class NColorTest
{
    [TestMethod]
    public void FromArgbUsesAlphaFirstOrder()
    {
        var color = NColor.FromArgb(10d, 20d, 30d, 40d);

        Assert.AreEqual(10f, color.A);
        Assert.AreEqual(20f, color.R);
        Assert.AreEqual(30f, color.G);
        Assert.AreEqual(40f, color.B);
    }

    [TestMethod]
    public void WithAlphaKeepsRgbChannels()
    {
        var color = new NColor(20d, 30d, 40d).WithAlpha(128d);

        Assert.AreEqual(128f, color.A);
        Assert.AreEqual(20f, color.R);
        Assert.AreEqual(30f, color.G);
        Assert.AreEqual(40f, color.B);
    }

    [TestMethod]
    public void LerpInterpolatesAndRoundsChannels()
    {
        var color = NColor.Lerp(new NColor(0d, 0d, 0d), new NColor(10d, 20d, 30d), 0.5d);

        Assert.AreEqual(5f, color.R);
        Assert.AreEqual(10f, color.G);
        Assert.AreEqual(15f, color.B);
        Assert.AreEqual(255f, color.A);
    }

    [TestMethod]
    public void ObjectConstructorAcceptsWpfBrush()
    {
        var color = new NColor(new SolidColorBrush(Color.FromArgb(8, 1, 2, 3)));

        Assert.AreEqual(8f, color.A);
        Assert.AreEqual(1f, color.R);
        Assert.AreEqual(2f, color.G);
        Assert.AreEqual(3f, color.B);
    }
}