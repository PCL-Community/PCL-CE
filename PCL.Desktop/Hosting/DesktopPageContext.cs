// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Hosting;

internal sealed record DesktopPageContext(
    Func<DesktopMainPage> CreateLaunchPage,
    Func<DesktopMainPage> CreateDownloadPage,
    Func<DesktopMainPage> CreateCommunityPage,
    Func<DesktopMainPage> CreateSettingsPage,
    Func<string, DesktopMainPage> CreatePlaceholderPage);
