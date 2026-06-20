// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Libraries;

public sealed record MinecraftLibraryToken
{
    public string? OriginalName { get; init; }
    public string? NameWithoutVersion { get; init; }
    public string? Url { get; init; }
    public required string LocalPath { get; init; }
    public string? Sha1 { get; init; }
    public long Size { get; init; }
    public bool IsNatives { get; init; }
    public bool IsLocal { get; init; }
}
