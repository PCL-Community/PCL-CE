// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;

namespace PCL.Application.Minecraft.Launch.Arguments;

public sealed record MinecraftJvmArgumentRequest
{
    public required JsonObject VersionJson { get; init; }
    public IReadOnlyList<JsonObject> InheritedVersionJsons { get; init; } = [];
    public required MinecraftArgumentRuleContext RuleContext { get; init; }
    public required string MainClass { get; init; }
    public string? CustomJvmArguments { get; init; }
    public int MemoryMegabytes { get; init; }
    public string? NativesDirectory { get; init; }
    public MinecraftJvmIpPreference PreferredIpStack { get; init; }
    public IReadOnlyList<string> PrefixArguments { get; init; } = [];
    public IReadOnlyList<string> SuffixArguments { get; init; } = [];
    public bool UseModernArguments { get; init; }
}

public enum MinecraftJvmIpPreference
{
    SystemDefault,
    PreferV4,
    PreferV6
}
