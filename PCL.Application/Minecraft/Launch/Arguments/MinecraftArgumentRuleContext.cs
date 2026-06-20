// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Arguments;

public sealed record MinecraftArgumentRuleContext
{
    public required MinecraftArgumentOperatingSystem OperatingSystem { get; init; }
    public string OperatingSystemVersion { get; init; } = string.Empty;
    public bool Is32BitArchitecture { get; init; }
    public bool EnableQuickPlayFeatureArguments { get; init; }
}
