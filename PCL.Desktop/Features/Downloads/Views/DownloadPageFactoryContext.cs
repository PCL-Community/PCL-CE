// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Downloads.Views;

internal sealed class DownloadPageFactoryContext(Func<PageDownloadInstall>? installFactory = null)
{
    private readonly Func<PageDownloadInstall> _installFactory = installFactory ?? (static () => new PageDownloadInstall());
    private PageDownloadInstall? _installPage;

    public PageDownloadInstall CreateInstallPage() => _installPage ??= _installFactory();
}
