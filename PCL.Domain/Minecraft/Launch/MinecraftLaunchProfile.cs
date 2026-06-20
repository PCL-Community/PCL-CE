// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Domain.Minecraft.Launch;

public sealed record MinecraftLaunchProfile
{
    public required string InstanceId { get; init; }
    public Version? VanillaVersion { get; init; }
    public bool HasReliableVanillaVersion { get; init; }
    public DateTimeOffset? ReleaseTime { get; init; }
    public int? ManifestJavaMajorVersion { get; init; }
    public string? ManifestJavaComponent { get; init; }
    public bool HasOptiFine { get; init; }
    public bool HasForge { get; init; }
    public string? ForgeVersion { get; init; }
    public bool HasCleanroom { get; init; }
    public string? CleanroomVersion { get; init; }
    public bool HasFabric { get; init; }
    public bool HasLiteLoader { get; init; }
    public bool HasLabyMod { get; init; }
}
