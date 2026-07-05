// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Downloads.Views;

internal readonly record struct DownloadVersionFilterDescriptor(
    DownloadVersionFilter Filter,
    string ItemName);

internal static class DownloadVersionFilterRegistry
{
    private static readonly DownloadVersionFilterDescriptor[] Descriptors =
    [
        new(DownloadVersionFilter.All, "ItemAll"),
        new(DownloadVersionFilter.Release, "ItemRelease"),
        new(DownloadVersionFilter.Snapshot, "ItemSnapshot"),
        new(DownloadVersionFilter.BeforeRelease, "ItemBeforeRelease"),
        new(DownloadVersionFilter.AprilFools, "ItemAprilFools")
    ];

    public static ReadOnlySpan<DownloadVersionFilterDescriptor> Items => Descriptors;

    public static DownloadVersionFilter Normalize(int value)
    {
        DownloadVersionFilter filter = (DownloadVersionFilter)value;
        return Normalize(filter);
    }

    public static DownloadVersionFilter Normalize(DownloadVersionFilter filter) =>
        IsDefined(filter) ? filter : DownloadVersionFilter.All;

    public static bool IsDefined(DownloadVersionFilter filter) =>
        filter is DownloadVersionFilter.All
            or DownloadVersionFilter.Release
            or DownloadVersionFilter.Snapshot
            or DownloadVersionFilter.BeforeRelease
            or DownloadVersionFilter.AprilFools;
}
