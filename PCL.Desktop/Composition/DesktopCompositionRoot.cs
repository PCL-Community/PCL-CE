// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Minecraft.Downloads;
using PCL.Application.Logging;
using PCL.Core.Logging;
using PCL.Desktop.Models;
using PCL.Desktop.ViewModels;
using PCL.Platform.Abstractions.Paths;
using PCL.Platform.Abstractions.System;
using PCL.Platform.Paths;
using PCL.Platform.System;
using PCL.Desktop.Services;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Composition;

public static class DesktopCompositionRoot
{
    internal static DesktopApplicationContext CreateApplicationContext(
        Avalonia.Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        AvaloniaUiScheduler scheduler = new();
        InAppNotificationService notifications = new(scheduler);
        AvaloniaDialogService dialogs = new();
        AvaloniaFileDialogService fileDialogs = new();
        AvaloniaClipboardService clipboard = new();
        AvaloniaThemeService theme = new(application);
        AvaloniaIconService icons = AvaloniaIconService.Shared;
        PortableLauncherLogSource logs = new();
        MainWindowViewModel mainWindow = CreateMainWindowViewModel(
            new DefaultPlatformPathProvider(),
            new DefaultSystemInfoProvider(),
            notifications,
            dialogs,
            theme,
            logs,
            scheduler,
            clipboard,
            fileDialogs);
        PortableLog.Info(
            "Desktop",
            "Avalonia 桌面环境已初始化。");

        return new DesktopApplicationContext(
            mainWindow,
            theme,
            dialogs,
            fileDialogs,
            icons,
            notifications);
    }

    public static MainWindowViewModel CreateMainWindowViewModel() =>
        CreateMainWindowViewModel(
            new DefaultPlatformPathProvider(),
            new DefaultSystemInfoProvider(),
            CreateTestServices());

    public static MainWindowViewModel CreateMainWindowViewModel(
        IPlatformPathProvider pathProvider,
        ISystemInfoProvider systemInfoProvider) =>
        CreateMainWindowViewModel(
            pathProvider,
            systemInfoProvider,
            CreateTestServices());

    private static MainWindowViewModel CreateMainWindowViewModel(
        IPlatformPathProvider pathProvider,
        ISystemInfoProvider systemInfoProvider,
        TestPresentationServices services) =>
        CreateMainWindowViewModel(
            pathProvider,
            systemInfoProvider,
            services.Notifications,
            services.Dialogs,
            services.Theme,
            services.Logs,
            services.Scheduler,
            services.Clipboard,
            services.FileDialogs);

    private static MainWindowViewModel CreateMainWindowViewModel(
        IPlatformPathProvider pathProvider,
        ISystemInfoProvider systemInfoProvider,
        InAppNotificationService notifications,
        IDialogService dialogs,
        IThemeService theme,
        ILauncherLogSource logs,
        IUiScheduler scheduler,
        IClipboardService clipboard,
        IFileDialogService fileDialogs)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(systemInfoProvider);

        OperatingSystemInfo operatingSystem = systemInfoProvider.GetOperatingSystem();
        CpuInfo cpu = systemInfoProvider.GetCpuInfo();
        MemoryInfo memory = systemInfoProvider.GetMemoryInfo();

        DesktopEnvironmentSnapshot environment = new(
            OperatingSystem.IsMacOS(),
            $"{operatingSystem.Name} · {operatingSystem.Architecture}",
            $"{cpu.Architecture} · {cpu.LogicalProcessorCount} 个逻辑处理器",
            FormatBytes(memory.TotalBytes),
            Path.Combine(pathProvider.ApplicationDataDirectory, "PCL N"),
            Path.Combine(pathProvider.CacheDirectory, "PCL N"),
            pathProvider.TemporaryDirectory);

        return new MainWindowViewModel(
            environment,
            notifications,
            dialogs,
            theme,
            logs,
            scheduler,
            clipboard,
            fileDialogs);
    }

    public static bool ValidateEnvironment()
    {
        using MainWindowViewModel viewModel = CreateMainWindowViewModel();
        string[] downloadSources = MinecraftDownloadSourcePlanner.GetLauncherOrMetaSources(
            "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",
            preferOfficialSource: true);

        return viewModel.NavigationItems.Count > 0 &&
               downloadSources.Length > 0 &&
               !string.IsNullOrWhiteSpace(viewModel.Environment.ApplicationDataDirectory) &&
               !string.IsNullOrWhiteSpace(viewModel.Environment.CacheDirectory);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "由系统动态管理";

        const double gibibyte = 1024d * 1024d * 1024d;
        return $"{bytes / gibibyte:F1} GiB 可用上限";
    }

    private static TestPresentationServices CreateTestServices()
    {
        InlineUiScheduler scheduler = new();
        return new TestPresentationServices(
            new InAppNotificationService(scheduler),
            new NullDialogService(),
            new NullThemeService(),
            new PortableLauncherLogSource(),
            scheduler,
            new NullClipboardService(),
            new NullFileDialogService());
    }

    private sealed record TestPresentationServices(
        InAppNotificationService Notifications,
        IDialogService Dialogs,
        IThemeService Theme,
        ILauncherLogSource Logs,
        IUiScheduler Scheduler,
        IClipboardService Clipboard,
        IFileDialogService FileDialogs);

    private sealed class InlineUiScheduler : IUiScheduler
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();

        public Task InvokeAsync(
            Action action,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class NullDialogService : IDialogService
    {
        public Task ShowMessageAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<string?> PromptAsync(
            string title,
            string message,
            string? defaultValue = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class NullThemeService : IThemeService
    {
        public ThemeMode CurrentMode => ThemeMode.System;

        public AccentColor CurrentAccent => AccentColor.CatBlue;

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged
        {
            add { }
            remove { }
        }

        public void Apply(ThemeMode mode, AccentColor accent)
        {
        }
    }

    private sealed class NullClipboardService : IClipboardService
    {
        public Task SetTextAsync(
            string text,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NullFileDialogService : IFileDialogService
    {
        public Task<string?> PickSaveFileAsync(
            string title,
            string suggestedFileName,
            IReadOnlyList<FileDialogFilter> filters,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickOpenFileAsync(
            string title,
            IReadOnlyList<FileDialogFilter> filters,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
