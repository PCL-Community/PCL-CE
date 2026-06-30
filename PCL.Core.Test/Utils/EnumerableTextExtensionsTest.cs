using System.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Test.Utils;

[TestClass]
public class EnumerableTextExtensionsTest
{
    [TestMethod]
    public void JoinSkipsNullElementsAndKeepsSeparators()
    {
        IEnumerable values = new object?[] { "a", null, "b" };

        Assert.AreEqual("a||b", values.Join("|"));
    }
}