using Microsoft.VisualStudio.TestTools.UnitTesting;

using static PCL.Core.UI.ToastNotification;

namespace PCL.Core.Test;

[TestClass]
public class ToastTest
{
    [TestMethod]
    public void TestToast()
    {
        // 别跑会炸，因为 Basics.cs 的 Metadata 需要在运行时加载，单元测试项目无法访问
        SendToast("A toast notice from PCL.Core!", "Test Toast");
    }
}