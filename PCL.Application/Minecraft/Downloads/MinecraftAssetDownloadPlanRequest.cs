// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Minecraft.Assets;

namespace PCL.Application.Minecraft.Downloads;

public sealed record MinecraftAssetDownloadPlanRequest
{
    public required IReadOnlyList<MinecraftAssetToken> Assets { get; init; }
    public bool CheckHash { get; init; }
    public IReadOnlyDictionary<string, MinecraftAssetFileState> ExistingFiles { get; init; } =
        new Dictionary<string, MinecraftAssetFileState>(StringComparer.Ordinal);
}
