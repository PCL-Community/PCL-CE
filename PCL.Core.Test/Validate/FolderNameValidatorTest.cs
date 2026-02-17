using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public class FolderNameValidatorTest
{
    public TestContext TestContext { get; set; }
    
    [TestMethod]
    [DataRow(" 测试 ", false)]
    [DataRow("Folder.", false)]
    [DataRow("LPT4", false)]
    [DataRow("给我一首歌的~1", false)]
    [DataRow("1|1|4|5|1|4", false)]
    [DataRow("不懂爱恨情仇煎熬的我们", true)]
    [DataRow("关注洛天依LuoTianyi0712谢谢喵!!!", true)]
    [DataRow("关注初音未来初音ミクHatsuneMiku0831谢谢喵!!!", true)]
    public void TestFolderNameValidate(string folderPath, bool expected)
    {
        var validator = new FolderNameValidator();
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