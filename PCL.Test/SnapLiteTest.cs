using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.FileVersionControl;
using System.Threading.Tasks;

namespace PCL.Test;

[TestClass]
public class SnapLiteTest
{
    [TestMethod]
    public async Task TestMake()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "PCLTest", "SnapLiteTest");
        Directory.CreateDirectory(tempFolder);
        using var snap = new LiteSnapVersionControl(tempFolder);
        await snap.CreateNewVersion();
    }
}