// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Desktop.Services;
using PCL.Desktop.ViewModels;

namespace PCL.Desktop.Composition;

internal sealed record DesktopApplicationContext(
    MainWindowViewModel MainWindow,
    AvaloniaThemeService ThemeService,
    AvaloniaDialogService DialogService,
    AvaloniaFileDialogService FileDialogService,
    AvaloniaIconService IconService,
    InAppNotificationService NotificationService);
