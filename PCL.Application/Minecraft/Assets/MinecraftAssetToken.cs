// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Assets;

public sealed record MinecraftAssetToken
{
    public required string LocalPath { get; init; }
    public required string SourcePath { get; init; }
    public required string Hash { get; init; }
    public long Size { get; init; }
}
