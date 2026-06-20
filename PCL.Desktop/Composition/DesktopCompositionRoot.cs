// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Minecraft.Downloads;
using PCL.Desktop.Models;
using PCL.Desktop.ViewModels;
using PCL.Platform.Abstractions.Paths;
using PCL.Platform.Abstractions.System;
using PCL.Platform.Paths;
using PCL.Platform.System;

namespace PCL.Desktop.Composition;

public static class DesktopCompositionRoot
{
    public static MainWindowViewModel CreateMainWindowViewModel() =>
        CreateMainWindowViewModel(
            new DefaultPlatformPathProvider(),
            new DefaultSystemInfoProvider());

    public static MainWindowViewModel CreateMainWindowViewModel(
        IPlatformPathProvider pathProvider,
        ISystemInfoProvider systemInfoProvider)
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

        return new MainWindowViewModel(environment);
    }

    public static bool ValidateEnvironment()
    {
        MainWindowViewModel viewModel = CreateMainWindowViewModel();
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
}
