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

        using SingleInstanceCoordinator singleInstance = SingleInstanceCoordinator.Create();
        if (!singleInstance.IsPrimaryInstance)
            return singleInstance.SignalExistingInstance();

        App.SingleInstanceCoordinator = singleInstance;
        singleInstance.StartListening();
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
        var assetLoader = new StandardAssetLoader(typeof(Program).Assembly);
        return ValidateResource(assetLoader, "avares://PCL.Desktop/Assets/icon.png") &&
               ValidateResource(assetLoader, "avares://PCL.Desktop/Assets/icon.ico") &&
               ValidateResource(assetLoader, "avares://PCL.Desktop/WpfOriginal/Images/icon.png")
            ? 0
            : 1;
    }

    private static bool ValidateResource(StandardAssetLoader assetLoader, string resourceUri)
    {
        var uri = new Uri(resourceUri, UriKind.Absolute);
        if (assetLoader.Exists(uri))
            return true;

        Console.Error.WriteLine($"Missing Avalonia resource: {resourceUri}");
        return false;
    }
}
