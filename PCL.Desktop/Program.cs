// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Platform;
using PCL.Desktop.Composition;

namespace PCL.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is ["--validate-environment"])
            return DesktopCompositionRoot.ValidateEnvironment() ? 0 : 1;
        if (args is ["--validate-assets"])
        {
            IAssetLoader assetLoader =
                new StandardAssetLoader(typeof(Program).Assembly);
            return Services.AvaloniaIconService.ValidateResources(assetLoader)
                ? 0
                : 1;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
