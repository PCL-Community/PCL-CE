// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Downloads;

public sealed record MinecraftAssetIndexDownloadPlan
{
    public string? IndexId { get; init; }
    public string? Url { get; init; }
    public string? LocalPath { get; init; }
    public bool UsedLegacyFallback { get; init; }
    public bool HasDownload => !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(LocalPath);
}
