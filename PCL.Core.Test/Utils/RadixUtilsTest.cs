using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils;

namespace PCL.Core.Test.Utils;

[TestClass]
public class RadixUtilsTest
{
    [TestMethod]
    public void ConvertsLargeNumbersWithBigInteger()
    {
        const string input = "999999999999999999999999999999";
        var encoded = RadixUtils.Convert(input, 10, 65);
        var decoded = RadixUtils.Convert(encoded, 65, 10);
        Assert.AreEqual(input, decoded);
    }

    [TestMethod]
    public void ConvertsNegativeNumbers()
    {
        Assert.AreEqual("-11111111", RadixUtils.Convert("-255", 10, 2));
    }
}