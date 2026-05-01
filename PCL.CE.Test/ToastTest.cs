using Microsoft.VisualStudio.TestTools.UnitTesting;

using static PCL.CE.Core.UI.ToastNotification;

namespace PCL.CE.Test;

[TestClass]
public class ToastTest
{
    [TestMethod]
    public void TestToast()
    {
        SendToast("A toast notice from PCL.CE.Core!", "Test Toast");
    }
}