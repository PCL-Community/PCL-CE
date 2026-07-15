using PCL.Platform.Abstractions.Security;
using PCL.Platform.Paths;
using PCL.Platform.Security;

namespace PCL.Platform.Test;

[TestClass]
public sealed class SecureStorageTests
{
    [TestMethod]
    public async Task DefaultSecureStorage_RoundTripsAndDeletesOnWindows()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("Windows DPAPI contract test.");
        string root = Path.Combine(Path.GetTempPath(), "pcl-secure-storage-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            DefaultSecureStorage storage = new(root, "PCL-N-Test");
            byte[] first = "first"u8.ToArray();
            byte[] second = "second"u8.ToArray();
            Assert.AreEqual(SecureStorageStatus.Success, (await storage.WriteAsync("plugin/test/token", first)).Status);
            CollectionAssert.AreEqual(first, (await storage.ReadAsync("plugin/test/token")).Value);
            Assert.AreEqual(SecureStorageStatus.Success, (await storage.WriteAsync("plugin/test/token", second)).Status);
            CollectionAssert.AreEqual(second, (await storage.ReadAsync("plugin/test/token")).Value);
            Assert.IsTrue((await storage.DeleteAsync("plugin/test/token")).IsSuccess);
            Assert.AreEqual(SecureStorageStatus.NotFound, (await storage.ReadAsync("plugin/test/token")).Status);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
