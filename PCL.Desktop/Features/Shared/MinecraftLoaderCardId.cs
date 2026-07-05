// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Shared;

internal readonly record struct MinecraftLoaderCardId(string Value)
{
    public static readonly MinecraftLoaderCardId Forge = new("Forge");
    public static readonly MinecraftLoaderCardId Cleanroom = new("Cleanroom");
    public static readonly MinecraftLoaderCardId NeoForge = new("NeoForge");
    public static readonly MinecraftLoaderCardId Fabric = new("Fabric");
    public static readonly MinecraftLoaderCardId LegacyFabric = new("LegacyFabric");
    public static readonly MinecraftLoaderCardId FabricApi = new("FabricApi");
    public static readonly MinecraftLoaderCardId LegacyFabricApi = new("LegacyFabricApi");
    public static readonly MinecraftLoaderCardId Quilt = new("Quilt");
    public static readonly MinecraftLoaderCardId Qsl = new("QSL");
    public static readonly MinecraftLoaderCardId LabyMod = new("LabyMod");
    public static readonly MinecraftLoaderCardId OptiFine = new("OptiFine");
    public static readonly MinecraftLoaderCardId OptiFabric = new("OptiFabric");
    public static readonly MinecraftLoaderCardId LiteLoader = new("LiteLoader");

    public override string ToString() => Value;
}
