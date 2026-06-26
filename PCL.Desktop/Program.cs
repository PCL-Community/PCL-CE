// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;

namespace PCL.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--validate-environment", StringComparer.OrdinalIgnoreCase))
            return ValidateEnvironment();
        if (args.Contains("--validate-assets", StringComparer.OrdinalIgnoreCase))
            return ValidateAssets();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static int ValidateEnvironment()
    {
        return OperatingSystem.IsWindows() ||
               OperatingSystem.IsLinux() ||
               OperatingSystem.IsMacOS()
            ? 0
            : 1;
    }

    private static int ValidateAssets()
    {
        string baseDirectory = AppContext.BaseDirectory;
        return File.Exists(Path.Combine(baseDirectory, "PCL.Desktop.dll")) &&
               File.Exists(Path.Combine(baseDirectory, "PCL.Desktop.deps.json"))
            ? 0
            : 1;
    }
}
