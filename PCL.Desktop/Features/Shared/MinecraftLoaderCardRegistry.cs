// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Shared;

internal static class MinecraftLoaderCardRegistry
{
    private static readonly MinecraftLoaderCardDescriptor[] Cards =
    [
        new(MinecraftLoaderCardId.Forge),
        new(MinecraftLoaderCardId.Cleanroom),
        new(MinecraftLoaderCardId.NeoForge),
        new(MinecraftLoaderCardId.Fabric),
        new(MinecraftLoaderCardId.LegacyFabric),
        new(MinecraftLoaderCardId.FabricApi),
        new(MinecraftLoaderCardId.LegacyFabricApi),
        new(MinecraftLoaderCardId.Quilt),
        new(MinecraftLoaderCardId.Qsl),
        new(MinecraftLoaderCardId.LabyMod),
        new(MinecraftLoaderCardId.OptiFine),
        new(MinecraftLoaderCardId.OptiFabric),
        new(MinecraftLoaderCardId.LiteLoader)
    ];

    public static ReadOnlySpan<MinecraftLoaderCardDescriptor> AllCards => Cards;
}
