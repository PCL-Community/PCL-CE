// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;

namespace PCL.Application.Minecraft.Assets;

public sealed record MinecraftAssetIndexRequest
{
    public required JsonObject VersionJson { get; init; }
    public IReadOnlyList<JsonObject> InheritedVersionJsons { get; init; } = [];
    public bool UseLegacyFallback { get; init; }
    public bool AllowUrlOnlyAssetIndex { get; init; }
}
