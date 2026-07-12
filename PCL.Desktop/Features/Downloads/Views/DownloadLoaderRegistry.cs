// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Downloads;
using PCL.Desktop.Features.Shared;

namespace PCL.Desktop.Features.Downloads.Views;

internal readonly record struct DownloadLoaderDescriptor(
    MinecraftLoaderKind Kind,
    MinecraftLoaderCardId CardId,
    string DisplayName,
    string Logo)
{
    public string CardName => CardId.Value;
}

internal static class DownloadLoaderRegistry
{
    private static readonly DownloadLoaderDescriptor[] Descriptors =
    [
        new(
            MinecraftLoaderKind.Forge,
            MinecraftLoaderCardId.Forge,
            "Forge",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/Anvil.png"),
        new(
            MinecraftLoaderKind.Cleanroom,
            MinecraftLoaderCardId.Cleanroom,
            "Cleanroom",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/Cleanroom.png"),
        new(
            MinecraftLoaderKind.NeoForge,
            MinecraftLoaderCardId.NeoForge,
            "NeoForge",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/NeoForge.png"),
        new(
            MinecraftLoaderKind.Fabric,
            MinecraftLoaderCardId.Fabric,
            "Fabric",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/Fabric.png"),
        new(
            MinecraftLoaderKind.LegacyFabric,
            MinecraftLoaderCardId.LegacyFabric,
            "Legacy Fabric",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/Fabric.png"),
        new(
            MinecraftLoaderKind.Quilt,
            MinecraftLoaderCardId.Quilt,
            "Quilt",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/Quilt.png"),
        new(
            MinecraftLoaderKind.LabyMod,
            MinecraftLoaderCardId.LabyMod,
            "LabyMod",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/LabyMod.png"),
        new(
            MinecraftLoaderKind.OptiFine,
            MinecraftLoaderCardId.OptiFine,
            "OptiFine",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/GrassPath.png"),
        new(
            MinecraftLoaderKind.LiteLoader,
            MinecraftLoaderCardId.LiteLoader,
            "LiteLoader",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/Egg.png")
    ];

    public static ReadOnlySpan<DownloadLoaderDescriptor> All => Descriptors;

    public static bool TryGetByCardName(string? cardName, out DownloadLoaderDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            descriptor = default;
            return false;
        }

        foreach (DownloadLoaderDescriptor candidate in Descriptors)
        {
            if (string.Equals(candidate.CardId.Value, cardName, StringComparison.Ordinal))
            {
                descriptor = candidate;
                return true;
            }
        }

        descriptor = default;
        return false;
    }

    public static DownloadLoaderDescriptor Get(MinecraftLoaderKind kind)
    {
        foreach (DownloadLoaderDescriptor descriptor in Descriptors)
        {
            if (descriptor.Kind == kind)
                return descriptor;
        }

        string fallback = kind.ToString();
        return new DownloadLoaderDescriptor(kind, new MinecraftLoaderCardId(fallback), fallback, string.Empty);
    }

}

internal readonly record struct DownloadAddonDescriptor(
    MinecraftInstallAddonKind Kind,
    MinecraftLoaderCardId CardId,
    string DisplayName,
    string Logo)
{
    public string CardName => CardId.Value;
}

internal static class DownloadAddonRegistry
{
    private static readonly DownloadAddonDescriptor[] Descriptors =
    [
        new(MinecraftInstallAddonKind.FabricApi, MinecraftLoaderCardId.FabricApi, "Fabric API",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/Fabric.png"),
        new(MinecraftInstallAddonKind.LegacyFabricApi, MinecraftLoaderCardId.LegacyFabricApi, "Legacy Fabric API",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/Fabric.png"),
        new(MinecraftInstallAddonKind.Qsl, MinecraftLoaderCardId.Qsl, "QSL",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/Quilt.png"),
        new(MinecraftInstallAddonKind.OptiFabric, MinecraftLoaderCardId.OptiFabric, "OptiFabric",
            "avares://PCL.Desktop/Assets/Legacy/Blocks/OptiFabric.png")
    ];

    public static ReadOnlySpan<DownloadAddonDescriptor> All => Descriptors;

    public static bool TryGetByCardName(string? cardName, out DownloadAddonDescriptor descriptor)
    {
        foreach (DownloadAddonDescriptor candidate in Descriptors)
        {
            if (string.Equals(candidate.CardName, cardName, StringComparison.Ordinal))
            {
                descriptor = candidate;
                return true;
            }
        }

        descriptor = default;
        return false;
    }
}
