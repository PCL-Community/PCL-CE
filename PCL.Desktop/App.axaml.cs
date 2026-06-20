// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Composition;
using PCL.Desktop.Views;

namespace PCL.Desktop;

public sealed partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DesktopApplicationContext context =
                DesktopCompositionRoot.CreateApplicationContext(this);
            context.ThemeService.Apply(
                PCL.UI.Abstractions.ThemeMode.System,
                PCL.UI.Abstractions.AccentColor.CatBlue);
            desktop.MainWindow = new MainWindow(context.MainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
