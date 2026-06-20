// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Desktop.Composition;
using PCL.Desktop.ViewModels;
using PCL.Desktop.ViewModels.Tools;
using PCL.Desktop.ViewModels.Log;
using PCL.Platform.Abstractions.Paths;
using PCL.Platform.Abstractions.System;

namespace PCL.Desktop.Test;

[TestClass]
public sealed class DesktopCompositionRootTests
{
    [TestMethod]
    public void CreateMainWindowViewModel_UsesPortablePlatformProviders()
    {
        using MainWindowViewModel viewModel = DesktopCompositionRoot.CreateMainWindowViewModel(
            new TestPathProvider(),
            new TestSystemInfoProvider());

        Assert.AreEqual("Test OS · X64", viewModel.Environment.OperatingSystem);
        Assert.AreEqual(OperatingSystem.IsMacOS(), viewModel.Environment.IsMacOS);
        StringAssert.EndsWith(viewModel.Environment.ApplicationDataDirectory, "PCL N");
        StringAssert.EndsWith(viewModel.Environment.CacheDirectory, "PCL N");
        Assert.AreEqual("首页", viewModel.SelectedTitle);
    }

    [TestMethod]
    public void NavigationCommand_ChangesSelectedContent()
    {
        using MainWindowViewModel viewModel = DesktopCompositionRoot.CreateMainWindowViewModel(
            new TestPathProvider(),
            new TestSystemInfoProvider());

        NavigationItemViewModel plugin = viewModel.NavigationItems.Single(
            static item => item.IsComingSoon);
        plugin.OpenCommand.Execute(null);

        Assert.AreEqual("插件", viewModel.SelectedTitle);
        Assert.AreSame(plugin.Page, viewModel.CurrentPage);
        Assert.IsTrue(plugin.IsSelected);
        Assert.AreEqual(1, viewModel.NavigationItems.Count(static item => item.IsSelected));
    }

    [TestMethod]
    public void ControlsGalleryNavigation_UsesConcretePageViewModel()
    {
        using MainWindowViewModel viewModel =
            DesktopCompositionRoot.CreateMainWindowViewModel(
                new TestPathProvider(),
                new TestSystemInfoProvider());
        NavigationItemViewModel gallery = viewModel.NavigationItems.Single(
            static item => item.Page is ControlsGalleryViewModel);

        gallery.OpenCommand.Execute(null);

        Assert.AreEqual("界面组件", viewModel.SelectedTitle);
        Assert.IsInstanceOfType<ControlsGalleryViewModel>(
            viewModel.CurrentPage);
    }

    [TestMethod]
    public void LogNavigation_UsesPortableLogPageViewModel()
    {
        using MainWindowViewModel viewModel =
            DesktopCompositionRoot.CreateMainWindowViewModel(
                new TestPathProvider(),
                new TestSystemInfoProvider());
        NavigationItemViewModel log = viewModel.NavigationItems.Single(
            static item => item.Page is LogPageViewModel);

        log.OpenCommand.Execute(null);

        Assert.AreEqual("日志", viewModel.SelectedTitle);
        Assert.IsInstanceOfType<LogPageViewModel>(viewModel.CurrentPage);
    }

    private sealed class TestPathProvider : IPlatformPathProvider
    {
        public string ApplicationDataDirectory => Path.Combine(
            Path.GetTempPath(),
            "pcl-desktop-test-data");

        public string CacheDirectory => Path.Combine(
            Path.GetTempPath(),
            "pcl-desktop-test-cache");

        public string TemporaryDirectory => Path.GetTempPath();
    }

    private sealed class TestSystemInfoProvider : ISystemInfoProvider
    {
        public OperatingSystemInfo GetOperatingSystem() =>
            new("Test OS", "1.0", "X64", true);

        public MemoryInfo GetMemoryInfo() =>
            new(16L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024);

        public CpuInfo GetCpuInfo() =>
            new("Test CPU", 8, "X64");
    }
}
