using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils;

namespace PCL.Core.Test.Utils;

[TestClass]
public class ConcurrentListTest
{
    [TestMethod]
    public void EnumerationUsesSnapshot()
    {
        var list = new ConcurrentList<int>([1, 2]);
        var snapshot = list.ToList();

        list.Add(3);

        CollectionAssert.AreEqual(new[] { 1, 2 }, snapshot);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, list.ToList());
    }

    [TestMethod]
    public void RemoveAtUpdatesList()
    {
        var list = new ConcurrentList<string>(["a", "b", "c"]);

        list.RemoveAt(1);

        CollectionAssert.AreEqual(new[] { "a", "c" }, list.ToList());
    }
}