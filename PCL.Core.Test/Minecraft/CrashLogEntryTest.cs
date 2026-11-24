using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class CrashLogEntryTest
{
    [TestMethod]
    public void TestCollectorReadsLfLogLineByLine()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", Path.GetRandomFileName());
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "latest.log"), "line1\nline2\nline3", new UTF8Encoding(false));
        var context = new CrashAnalysisContext(0, folder);
        new CrashLogCollector(context).Collect(folder, null);
        Directory.Delete(folder, true);
        CollectionAssert.AreEqual(new[] { "line1", "line2", "line3" }, context.RawFiles.Single().Lines.ToArray());
    }
}