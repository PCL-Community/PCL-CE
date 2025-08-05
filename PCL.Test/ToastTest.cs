using Microsoft.VisualStudio.TestTools.UnitTesting;

using static PCL.Core.Native.ToastNotification;

namespace PCL.Test;

[TestClass]
public class ToastTest
{
    [TestMethod]
    public void TestToast()
    {
        SendToast("A toast notice from PCL.Core!", "Test Toast");
    }
}