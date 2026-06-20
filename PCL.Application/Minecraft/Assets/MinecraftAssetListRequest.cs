// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;

namespace PCL.Application.Minecraft.Assets;

public sealed record MinecraftAssetListRequest
{
    public required JsonObject IndexJson { get; init; }
    public required string MinecraftRootDirectory { get; init; }
    public required string InstanceDirectory { get; init; }
}
