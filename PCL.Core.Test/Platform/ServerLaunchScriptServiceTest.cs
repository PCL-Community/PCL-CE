using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;
using PCL.Platform;

namespace PCL.Core.Test.Platform;

[TestClass]
public sealed class ServerLaunchScriptServiceTest
{
    [TestMethod]
    public void WindowsScriptUsesBatchCommands()
    {
        var service = new WindowsServerLaunchScriptService();
        var script = service.CreateScript(CreateOptions());

        Assert.AreEqual(".bat", service.FileExtension);
        StringAssert.Contains(script, "@echo off");
        StringAssert.Contains(script, "title Test Server");
        StringAssert.Contains(script, "cd /D \"%~dp0\"");
        StringAssert.Contains(script, "\"java\"");
        StringAssert.Contains(script, "pause");
        StringAssert.Contains(script, "\r\n");
        Assert.IsFalse(script.Contains("\n\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PosixScriptUsesShellCommands()
    {
        var service = new PosixServerLaunchScriptService(".sh");
        var script = service.CreateScript(CreateOptions());

        Assert.AreEqual(".sh", service.FileExtension);
        StringAssert.StartsWith(script, "#!/bin/sh\n");
        StringAssert.Contains(script, "cd \"$(dirname \"$0\")\"");
        StringAssert.Contains(script, "JAVA_BIN=");
        StringAssert.Contains(script, "read -r _");
        Assert.IsFalse(script.Contains('\r'));
    }

    [TestMethod]
    public void MacOsUsesCommandExtension()
    {
        var service = new PosixServerLaunchScriptService(".command");
        Assert.AreEqual(".command", service.FileExtension);
    }

    [TestMethod]
    public void PosixMakeExecutableAddsExecuteBits()
    {
        if (OperatingSystem.IsWindows()) return;

        var path = Path.Combine(
            Path.GetTempPath(),
            "pcl-server-script-" + Guid.NewGuid().ToString("N") + ".sh");
        File.WriteAllText(path, "#!/bin/sh\n");
        try
        {
            new PosixServerLaunchScriptService(".sh").MakeExecutable(path);
            var mode = File.GetUnixFileMode(path);
            Assert.IsTrue(mode.HasFlag(UnixFileMode.UserExecute));
            Assert.IsTrue(mode.HasFlag(UnixFileMode.GroupExecute));
            Assert.IsTrue(mode.HasFlag(UnixFileMode.OtherExecute));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ServerLaunchScriptOptions CreateOptions()
    {
        return new ServerLaunchScriptOptions(
            "1.21.1-server.jar",
            "Test Server",
            ["EULA instruction"],
            "Server stopped.");
    }
}