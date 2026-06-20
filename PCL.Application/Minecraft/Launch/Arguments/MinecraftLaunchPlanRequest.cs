// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Arguments;

public sealed record MinecraftLaunchPlanRequest
{
    public MinecraftJvmArgumentRequest? Jvm { get; init; }
    public string? PrebuiltJvmArguments { get; init; }
    public MinecraftLegacyGameArgumentRequest? LegacyGame { get; init; }
    public MinecraftModernGameArgumentRequest? ModernGame { get; init; }
    public required IReadOnlyDictionary<string, string> Replacements { get; init; }
    public int JavaMajorVersion { get; init; }
    public bool Fullscreen { get; init; }
    public IReadOnlyList<string> ExtraArguments { get; init; } = [];
    public string? CustomGameArguments { get; init; }
    public string? WorldName { get; init; }
    public string? Server { get; init; }
    public DateTimeOffset? ReleaseTime { get; init; }
    public bool HasOptiFine { get; init; }
}
