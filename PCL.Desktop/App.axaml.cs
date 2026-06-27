// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Desktop.Views;

namespace PCL.Desktop;

public sealed partial class App : Avalonia.Application
{
    private SplashWindow? _splashWindow;

    internal static SingleInstanceCoordinator? SingleInstanceCoordinator { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _splashWindow = new SplashWindow();
            _splashWindow.Show();

            MainWindow mainWindow = new();
            mainWindow.Opened += (_, _) => _splashWindow?.CloseWithFade(TimeSpan.FromMilliseconds(400));
            SingleInstanceCoordinator?.ActivationRequested += (_, _) =>
                Dispatcher.UIThread.Post(mainWindow.ActivateExistingInstance);
            if (SingleInstanceCoordinator?.ConsumePendingActivation() == true)
                Dispatcher.UIThread.Post(mainWindow.ActivateExistingInstance);

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
