using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public class FolderPathValidatorTest
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public TestContext TestContext { get; set; }
    
    [TestMethod]
    [DataRow(@"C:\Program Files", true)]
    [DataRow(@"C:\Windows\System32", true)]
    [DataRow(@"C:\Invalid|Folder", false)]
    [DataRow(@" C:\Test ", false)]
    [DataRow(@"C:\不该 (fe~3", false)]
    [DataRow(@"C:\CON\AUX", false)]
    [DataRow(@"C:\权御天下\关注洛天依LuoTianyi0712谢谢喵!!!", true)]
    [DataRow(@"C:\初音ミクの消失\关注初音未来初音ミクHatsuneMiku0831谢谢喵!!!", true)]
    public void TestFolderPathValidate(string folderPath, bool expected)
    {
        var validator = new FolderPathValidator();
        var result = validator.Validate(folderPath);
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