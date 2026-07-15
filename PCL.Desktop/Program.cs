// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Platform;
using PCL.Desktop.Diagnostics;
using PCL.Desktop.Hosting;
using PCL.Desktop.Platform;

namespace PCL.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Catch process-wide crashes as early as possible.
        UnhandledExceptionGuard.Install();

        try
        {
            // Apply CI-embedded secrets (MS client id, etc.) before any auth/UI code runs.
            PclEmbeddedSecrets.ApplyToEnvironment();

            if (args.Contains("--validate-environment", StringComparer.OrdinalIgnoreCase))
                return ValidateEnvironment();
            if (args.Contains("--validate-assets", StringComparer.OrdinalIgnoreCase))
                return ValidateAssets();
            if (args.Contains("--validate-secrets", StringComparer.OrdinalIgnoreCase))
                return PclEmbeddedSecrets.Count > 0 ? 0 : 2;
            if (args.Contains("--validate-plugin", StringComparer.OrdinalIgnoreCase))
            {
                DesktopHost.Initialize();
                return DesktopHost.Current.ModuleIds.Count > 0 && DesktopHost.Current.SettingsPages.Pages.Count > 0
                    ? 0
                    : 1;
            }

            EmbeddedRuntimeExtensionLoader.Load();

            using SingleInstanceCoordinator singleInstance = SingleInstanceCoordinator.Create();
            if (!singleInstance.IsPrimaryInstance)
                return singleInstance.SignalExistingInstance();

            App.SingleInstanceCoordinator = singleInstance;
            singleInstance.StartListening();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            UnhandledExceptionGuard.Report(ex, "Program.Main", canContinue: false);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static int ValidateEnvironment()
    {
        return DesktopPlatformApi.IsSupportedDesktopPlatform
            ? 0
            : 1;
    }

    private static int ValidateAssets()
    {
        var assetLoader = new StandardAssetLoader(typeof(Program).Assembly);
        return ValidateResource(assetLoader, "avares://PCL.Desktop/Assets/icon.png") &&
               ValidateResource(assetLoader, "avares://PCL.Desktop/Assets/icon.ico") &&
               ValidateResource(assetLoader, "avares://PCL.Desktop/Assets/Legacy/icon.png")
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
