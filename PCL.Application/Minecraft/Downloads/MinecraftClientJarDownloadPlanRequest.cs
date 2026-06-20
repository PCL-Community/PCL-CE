// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;

namespace PCL.Application.Minecraft.Downloads;

public sealed record MinecraftClientJarDownloadPlanRequest
{
    public required JsonObject VersionJson { get; init; }
    public required string InstanceDirectory { get; init; }
    public required string VersionName { get; init; }
}
