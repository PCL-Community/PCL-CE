// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Shared;

internal static class MinecraftLoaderCardRegistry
{
    private static readonly string[] CardNames =
    [
        "Forge",
        "Cleanroom",
        "NeoForge",
        "Fabric",
        "LegacyFabric",
        "FabricApi",
        "LegacyFabricApi",
        "Quilt",
        "QSL",
        "LabyMod",
        "OptiFine",
        "OptiFabric",
        "LiteLoader"
    ];

    public static ReadOnlySpan<string> AllCardNames => CardNames;
}
