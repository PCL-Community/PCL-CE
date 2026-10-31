using Microsoft.VisualStudio.TestTools.UnitTesting;

using static PCL.Core.UI.ToastNotification;

namespace PCL.Core.Test;

[TestClass]
public class ToastTest
{
    [TestMethod]
    public void TestToast()
    {
        // 别跑会炸，因为 Basics.cs 的 Metadata 需要在运行时加载，单元测试项目无法访问。
        // 因此这里明确标记为“未执行”，而不是让它以“通过”的形式出现在结果里。
        Assert.Inconclusive("ToastNotification 依赖 Basics.Metadata 的运行时环境，当前无法在单元测试中执行");
        SendToast("A toast notice from PCL.Core!", "Test Toast");
    }
}