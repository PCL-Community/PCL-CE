using System;
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
    [DataRow("line1\r\nline2\r\nline3")]
    [DataRow("line1\nline2\nline3")]
    [DataRow("line1\rline2\rline3")]
    [DataRow("line1\r\nline2\nline3\rline4")]
    public void TestCollectorSplitsEveryLineEnding(string content)
    {
        var root = Path.Combine(Path.GetTempPath(), "PCLTest", Path.GetRandomFileName());
        var instanceDir = Path.Combine(root, "versions", "1.20.1");
        Directory.CreateDirectory(instanceDir);
        try
        {
            File.WriteAllText(Path.Combine(instanceDir, "latest.log"), content, new UTF8Encoding(false));
            var context = new CrashAnalysisContext(0, root);
            new CrashLogCollector(context) { CollectDebugLog = static () => false }.Collect(instanceDir, null);

            var expected = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            CollectionAssert.AreEqual(expected, context.RawFiles.Single().Lines.ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}