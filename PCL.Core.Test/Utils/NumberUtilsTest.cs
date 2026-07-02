using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils;

namespace PCL.Core.Test.Utils;

[TestClass]
public class NumberUtilsTest
{
    [TestMethod]
    [DataRow(-1d, 0)]
    [DataRow(0d, 0)]
    [DataRow(12.4d, 12)]
    [DataRow(12.6d, 13)]
    [DataRow(300d, 255)]
    public void ClampToByte(double value, int expected)
    {
        Assert.AreEqual((byte)expected, NumberUtils.ClampToByte(value));
    }

    [TestMethod]
    public void LerpRoundsToSixDigits()
    {
        Assert.AreEqual(0.333333d, NumberUtils.Lerp(0d, 1d, 1d / 3d));
    }

    [TestMethod]
    [DataRow("12.5", 12.5d)]
    [DataRow("not-a-number", 0d)]
    [DataRow("&", 0d)]
    [DataRow(null, 0d)]
    public void ParseDoubleOrZeroUsesExplicitParsing(string? value, double expected)
    {
        Assert.AreEqual(expected, NumberUtils.ParseDoubleOrZero(value));
    }
}