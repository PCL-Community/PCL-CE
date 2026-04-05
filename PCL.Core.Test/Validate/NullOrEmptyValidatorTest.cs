using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public class NullOrEmptyValidatorTest
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public TestContext TestContext { get; set; }
    
    [TestMethod]
    [DataRow("", false)]
    [DataRow(" ", true)]
    [DataRow("葱包99", true)]
    public void TestNullOrEmptyValidate(string input, bool expected)
    {
        var validator = new NullOrEmptyValidator();
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