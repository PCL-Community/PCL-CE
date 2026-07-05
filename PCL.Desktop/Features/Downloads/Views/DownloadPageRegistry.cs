// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Downloads.Views;

internal static partial class DownloadPageRegistry
{
    public static partial bool IsDefined(DownloadPageSubType page);

    public static partial MyPageRight CreatePage(DownloadPageSubType page, DownloadPageFactoryContext context);

    public static partial string GetTitle(DownloadPageSubType page);

    [DownloadPage(DownloadPageSubType.Install, "安装")]
    private static PageDownloadInstall CreateInstallPage(DownloadPageFactoryContext context) =>
        context.CreateInstallPage();
}
