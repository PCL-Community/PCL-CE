// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;

namespace PCL.Desktop.Hosting;

internal sealed record DesktopMainPage(
    Control? Left,
    Control Right,
    string? Title = null,
    Action? Activated = null);
