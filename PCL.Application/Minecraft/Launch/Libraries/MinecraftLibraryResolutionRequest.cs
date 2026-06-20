// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;

namespace PCL.Application.Minecraft.Launch.Libraries;

public sealed record MinecraftLibraryResolutionRequest
{
    public required JsonObject VersionJson { get; init; }
    public required string MinecraftRootDirectory { get; init; }
    public string? TargetInstanceDirectory { get; init; }
    public required MinecraftLibraryOperatingSystem OperatingSystem { get; init; }
    public bool Is64BitArchitecture { get; init; }
    public string OperatingSystemVersion { get; init; } = string.Empty;
}
