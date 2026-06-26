// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Platform;

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
        if (string.IsNullOrWhiteSpace(Environment.ProcessPath) ||
            !File.Exists(Environment.ProcessPath))
            return 1;

        var assetLoader = new StandardAssetLoader(typeof(Program).Assembly);
        return ValidateResource(assetLoader, "avares://PCL.Desktop/Assets/icon.png") &&
               ValidateResource(assetLoader, "avares://PCL.Desktop/WpfOriginal/Images/icon.png") &&
               ValidateResource(assetLoader, "avares://PCL.Desktop/Themes/PclTheme.axaml")
            ? 0
            : 1;
    }

    private static bool ValidateResource(StandardAssetLoader assetLoader, string resourceUri)
    {
        return assetLoader.Exists(new Uri(resourceUri, UriKind.Absolute));
    }
}
