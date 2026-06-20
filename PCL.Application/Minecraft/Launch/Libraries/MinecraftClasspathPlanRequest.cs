// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Libraries;

public sealed record MinecraftClasspathPlanRequest
{
    public required IReadOnlyList<MinecraftLibraryToken> Libraries { get; init; }
    public IReadOnlyList<string> ClasspathHeadEntries { get; init; } = [];
    public IReadOnlyList<string> BundledClasspathEntries { get; init; } = [];
    public bool HasCleanroom { get; init; }
}
