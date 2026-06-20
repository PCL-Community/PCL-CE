// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Libraries;

public sealed record MinecraftLibraryDownloadPlan(
    IReadOnlyList<MinecraftLibraryDownloadFile> DownloadFiles,
    IReadOnlyList<MinecraftBundledLibraryFile> BundledFiles,
    IReadOnlyList<string> SkippedLocalLibraries);

public sealed record MinecraftLibraryDownloadFile
{
    public required IReadOnlyList<string> Urls { get; init; }
    public required string LocalPath { get; init; }
    public long ActualSize { get; init; } = -1;
    public long ReportedSize { get; init; }
    public string? Sha1 { get; init; }
    public bool IgnoreSize { get; init; }
    public MinecraftLibraryDownloadNote Note { get; init; }
}

public enum MinecraftLibraryDownloadNote
{
    None,
    LabyModSizeIgnored
}

public sealed record MinecraftBundledLibraryFile
{
    public required string ResourceName { get; init; }
    public required string LocalPath { get; init; }
}
