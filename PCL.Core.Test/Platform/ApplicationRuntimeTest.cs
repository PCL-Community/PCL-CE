using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;

namespace PCL.Core.Test.Platform;

[TestClass]
public sealed class ApplicationRuntimeTest
{
    [TestInitialize]
    public void Initialize()
    {
        ApplicationRuntime.ResetForTests();
    }

    [TestCleanup]
    public void Cleanup()
    {
        ApplicationRuntime.ResetForTests();
    }

    [TestMethod]
    public void CurrentThrowsBeforeInitialize()
    {
        Assert.IsFalse(ApplicationRuntime.IsInitialized);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = ApplicationRuntime.Current);
    }

    [TestMethod]
    public void InitializeOnlyOnce()
    {
        var services = CreateServices();

        ApplicationRuntime.Initialize(services);

        Assert.IsTrue(ApplicationRuntime.IsInitialized);
        Assert.AreSame(services, ApplicationRuntime.Current);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ApplicationRuntime.Initialize(services));
    }

    private static ApplicationServices CreateServices()
    {
        // 只验证运行时状态，不在这里构造真实平台服务。
        return new ApplicationServices
        {
            Platform = null!,
            Ui = null!
        };
    }
}