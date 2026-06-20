// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;

namespace PCL.Application.Minecraft.Launch.Arguments;

public sealed record MinecraftLegacyGameArgumentRequest
{
    public required string MinecraftArguments { get; init; }
    public bool NeedsRetroWrapper { get; init; }
    public bool HasForge { get; init; }
    public bool HasLiteLoader { get; init; }
    public bool HasOptiFine { get; init; }
}

public sealed record MinecraftModernGameArgumentRequest
{
    public required JsonObject VersionJson { get; init; }
    public IReadOnlyList<JsonObject> InheritedVersionJsons { get; init; } = [];
    public required MinecraftArgumentRuleContext RuleContext { get; init; }
    public bool HasForge { get; init; }
    public bool HasLiteLoader { get; init; }
    public bool HasOptiFine { get; init; }
}

public sealed record MinecraftGameArgumentResult(
    string Arguments,
    OptiFineTweakerAdjustment OptiFineTweakerAdjustment);

public enum OptiFineTweakerAdjustment
{
    None,
    MovedForgeTweaker,
    ReplacedPlainTweaker
}
