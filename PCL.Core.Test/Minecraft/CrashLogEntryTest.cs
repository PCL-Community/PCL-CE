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
            var logPath = Path.Combine(instanceDir, "latest.log");
            File.WriteAllText(logPath, content, new UTF8Encoding(false));

            var expected = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            CollectionAssert.AreEqual(expected, CrashLogEntry.SplitLines(CrashFileIo.ReadText(logPath)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}