using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public class HttpValidatorTest
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public TestContext TestContext { get; set; }
    
    [TestMethod]
    [DataRow("https://suki.ink", true)]
    [DataRow("http://ce.pclc.cc", true)]
    [DataRow("-10", false)]
    [DataRow("ftp://www.example.com", false)]
    [DataRow(@"\\server\share", false)]
    [DataRow("米库打油", false)]
    public void TestHttpValidate(string input, bool expected)
    {
        var validator = new HttpValidator();
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