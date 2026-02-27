using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public class IntValidatorTest
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public TestContext TestContext { get; set; }
    
    [TestMethod]
    [DataRow("50", true)]
    [DataRow("114514", false)]
    [DataRow("-10", false)]
    [DataRow("1145141919810", false)]
    [DataRow("米库打油", false)]
    public void TestIntValidate(string input, bool expected)
    {
        var validator = new IntValidator(100, 0);
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