// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace PCL.Desktop.Services;

internal static class DesktopWindowProvider
{
    public static Window GetRequiredMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWindow
            })
        {
            return mainWindow;
        }

        throw new InvalidOperationException(
            "The desktop main window is not available.");
    }
}
