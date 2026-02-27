using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public class FileNameValidatorTest
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("foobar.txt", true)]
    [DataRow("CON.tar.gz", true)]
    [DataRow("LPT²", false)]
    [DataRow(" Test", false)]
    [DataRow("?foo.", false)]
    [DataRow("""\/:*?"<>|""", false)]
    [DataRow("PCLCE.exe.", false)]
    [DataRow("我落泪情绪零~1.MP3", false)]
    [DataRow("关注洛天依LuoTianyi0712谢谢喵!!!", true)]
    public void TestFileNameValidate(string fileName, bool expected)
    {
        var validator = new FileNameValidator();
        var result = validator.Validate(fileName);
        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                TestContext.WriteLine(error.ErrorMessage);
            }
        }
        Assert.AreEqual(expected, result.IsValid);
    }

    [TestMethod]
    [DataRow("explorer.exe", false)]
    [DataRow("notepad.exe", false)]
    [DataRow("foobar.txt", true)]
    [DataRow("关注初音未来初音ミクHatsuneMiku0831谢谢喵!!!", true)]
    public void TestFileNameValidateWithParentFolder(string fileName, bool expected)
    {
        var validator = new FileNameValidator("C:\\Windows");
        var result = validator.Validate(fileName);
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