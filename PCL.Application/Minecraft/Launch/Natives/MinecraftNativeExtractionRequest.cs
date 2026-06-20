// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Natives;

public sealed record MinecraftNativeExtractionRequest
{
    public required IReadOnlyList<string> ArchivePaths { get; init; }
    public required string TargetDirectory { get; init; }
    public required MinecraftNativeOperatingSystem OperatingSystem { get; init; }
    public bool DeleteUnknownFiles { get; init; } = true;
}
