using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public class BlacklistValidatorTest
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("", true)]
    [DataRow(" ", true)]
    [DataRow("葱包99", true)]
    [DataRow("关注洛天依谢谢喵", false)]
    [DataRow("关注初音未来谢谢喵", false)]
    [DataRow("初始之音响彻未来 华风夏韵洛水天依 初音未来洛天依", false)]
    public void TestBlacklistValidate(string input, bool expected)
    {
        var validator = new BlacklistValidator(["洛天依", "初音未来"]);
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