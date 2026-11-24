using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Platform;

namespace PCL.Core.Test.Platform;

[TestClass]
public sealed class LaunchEnvironmentTest
{
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void LinuxGpuPreferenceSetsPrimeVariables()
    {
        var startInfo = new ProcessStartInfo();

        var service = new LinuxLaunchEnvironmentService();
        Assert.IsTrue(service.IsSupported);

        service.ApplyGpuPreference(startInfo, true, _ => { });

        Assert.AreEqual("1", startInfo.Environment["DRI_PRIME"]);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void LinuxRendererSetsMesaVariables()
    {
        var startInfo = new ProcessStartInfo();

        new LinuxLaunchEnvironmentService().ApplyRenderer(startInfo, 1, _ => { });

        Assert.AreEqual("1", startInfo.Environment["LIBGL_ALWAYS_SOFTWARE"]);
        Assert.AreEqual("llvmpipe", startInfo.Environment["GALLIUM_DRIVER"]);
    }

    [TestMethod]
    public void UnsupportedLaunchEnvironmentDoesNothing()
    {
        var startInfo = new ProcessStartInfo();
        var service = new UnsupportedLaunchEnvironmentService();
        var logs = new List<string>();
        Assert.IsFalse(service.IsSupported);

        service.ApplyGpuPreference(startInfo, true, logs.Add);
        service.ApplyRenderer(startInfo, 3, logs.Add);

        Assert.IsFalse(startInfo.Environment.ContainsKey("DRI_PRIME"));
        Assert.IsFalse(startInfo.Environment.ContainsKey("LIBGL_ALWAYS_SOFTWARE"));
        Assert.IsEmpty(logs);
    }
}
