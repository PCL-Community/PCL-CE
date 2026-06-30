using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils;

namespace PCL.Core.Test.Utils;

[TestClass]
public class Base64UtilsTest
{
    [TestMethod]
    public void EncodesAndDecodesUtf8Text()
    {
        const string text = "Plain Craft Launcher 测试";
        var encoded = Base64Utils.EncodeString(text);
        Assert.AreEqual(text, Base64Utils.DecodeToString(encoded));
    }
}