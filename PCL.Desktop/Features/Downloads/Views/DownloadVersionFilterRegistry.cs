// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Downloads.Views;

internal static class DownloadVersionFilterRegistry
{
    public static DownloadVersionFilter Normalize(int value)
    {
        DownloadVersionFilter filter = (DownloadVersionFilter)value;
        return IsDefined(filter) ? filter : DownloadVersionFilter.All;
    }

    public static bool IsDefined(DownloadVersionFilter filter) =>
        filter is DownloadVersionFilter.All
            or DownloadVersionFilter.Release
            or DownloadVersionFilter.Snapshot
            or DownloadVersionFilter.BeforeRelease
            or DownloadVersionFilter.AprilFools;
}
