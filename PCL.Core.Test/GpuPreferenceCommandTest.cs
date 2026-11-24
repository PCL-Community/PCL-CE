using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;

namespace PCL.Test;

[TestClass]
public sealed class GpuPreferenceCommandTest
{
    [TestMethod]
    public void IgnoresNormalArguments()
    {
        var service = new FakeGpuPreferenceService();

        Assert.IsNull(GpuPreferenceCommand.TryExecute(["update"], service));
        Assert.IsNull(service.LastPath);
    }

    [TestMethod]
    public void ExecutesGpuCommandForExistingFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var service = new FakeGpuPreferenceService();

            var result = GpuPreferenceCommand.TryExecute(
                ["--gpu", path, "0"],
                service);

            Assert.AreEqual((int)LauncherCommon.ProcessReturnValues.TaskDone, result);
            Assert.AreEqual(Path.GetFullPath(path), service.LastPath);
            Assert.IsFalse(service.LastPreference);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void RejectsMissingOrRelativePath()
    {
        var service = new FakeGpuPreferenceService();

        Assert.AreEqual(
            (int)LauncherCommon.ProcessReturnValues.Fail,
            GpuPreferenceCommand.TryExecute(["--gpu", "missing-java.exe"], service));
        Assert.AreEqual(
            (int)LauncherCommon.ProcessReturnValues.Fail,
            GpuPreferenceCommand.TryExecute(["--gpu"], service));
        Assert.IsNull(service.LastPath);
    }

    private sealed class FakeGpuPreferenceService : IGpuPreferenceService
    {
        public string? LastPath { get; private set; }

        public bool LastPreference { get; private set; }
        public bool IsSupported { get; } = true;

        public void Set(string javaExecutablePath, bool preferHighPerformance)
        {
            LastPath = Path.GetFullPath(javaExecutablePath);
            LastPreference = preferHighPerformance;
        }
    }
}
