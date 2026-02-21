using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public class StringLengthValidatorTest
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("关注洛天依谢谢喵", true)]
    [DataRow("关注初音未来谢谢喵", true)]
    [DataRow("初始之音响彻未来 华风夏韵洛水天依 初音未来洛天依", true)]
    [DataRow("我是初音未来 这才是洛天依 擦亮你的眼睛 别傻傻分不清 我是初音未来 这才是洛天依 你们抓洛天依关我什么事情", false)]
    [DataRow("这是初音未来 我才是洛天依 擦亮你的眼睛 别傻傻分不清 这是初音未来 我才是洛天依 你们抓洛天依我就是洛天依", false)]
    public void TestBlacklistValidate(string input, bool expected)
    {
        var validator = new StringLengthValidator(0, 30);
        var result = validator.Validate(input);
        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                TestContext.WriteLine(error.ErrorMessage);
            }
        }
        Assert.AreEqual(expected, result.IsValid);
    }
}