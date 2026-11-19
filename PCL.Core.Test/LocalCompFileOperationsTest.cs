using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public sealed class LocalCompFileOperationsTest
{
    [TestMethod]
    public void CsvEscapeQuotesOnlyWhenNeeded()
    {
        Assert.AreEqual("plain", LocalCompFileOperations.CsvEscape("plain"));
        Assert.AreEqual("\"a,b\"", LocalCompFileOperations.CsvEscape("a,b"));
        Assert.AreEqual("\"a\"\"b\"", LocalCompFileOperations.CsvEscape("a\"b"));
        Assert.AreEqual("\"a\r\nb\"", LocalCompFileOperations.CsvEscape("a\r\nb"));
    }

    [TestMethod]
    public void SelectionStateTracksKeys()
    {
        var state = new LocalCompSelectionState();

        state.Add("a");
        state.Add("A");
        state.Add("b");

        Assert.AreEqual(2, state.Count);
        Assert.IsTrue(state.Contains("a"));
        Assert.IsTrue(state.Contains("B"));

        state.RemoveWhere(key => key == "b");
        Assert.IsFalse(state.Contains("b"));

        state.SetSelected(["a", "c"], false);
        Assert.AreEqual(0, state.Count);

        state.SetSelected(["a", "c"], true);
        Assert.AreEqual(2, state.Count);
    }
}