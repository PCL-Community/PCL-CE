using System;
using System.Collections.Generic;
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

    [TestMethod]
    public void ShutdownDisposesServiceProviderOnce()
    {
        var provider = new TestServiceProvider();
        var services = CreateServices(provider);

        ApplicationRuntime.Initialize(services);
        ApplicationRuntime.Shutdown();

        Assert.IsFalse(ApplicationRuntime.IsInitialized);
        Assert.AreEqual(1, provider.DisposeCount);

        ApplicationRuntime.Shutdown();
        Assert.AreEqual(1, provider.DisposeCount);
    }

    private static ApplicationServices CreateServices()
    {
        return CreateServices(null);
    }

    private static ApplicationServices CreateServices(TestServiceProvider? provider)
    {
        return new ApplicationServices
        {
            Platform = new PlatformModule
            {
                Kind = PlatformKind.Linux,
                Capabilities = PlatformCapabilities.Linux
            },
            Ui = new UiModule(),
            Services = provider
        };
    }

    private sealed class DisposableProxy : ISystemProxyProvider, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }

        public IReadOnlyList<SystemProxyEntry> GetSystemProxies()
        {
            return [];
        }

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }

    private sealed class DisposableTheme : ISystemThemeProvider, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }

        public bool IsAvailable => false;

        public bool IsDarkMode => false;

        public event EventHandler? ThemeChanged
        {
            add { }
            remove { }
        }
    }
}