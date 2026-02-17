using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public class HttpAndUncValidatorTest
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public TestContext TestContext { get; set; }
    
    [TestMethod]
    [DataRow("https://suki.ink", true)]
    [DataRow("http://ce.pclc.cc", true)]
    [DataRow("-10", false)]
    [DataRow("ftp://www.example.com", false)]
    [DataRow(@"\\server\share", true)]
    [DataRow("米库打油", false)]
    public void TestHttpAndUncValidate(string input, bool expected)
    {
        var validator = new HttpAndUncValidator();
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