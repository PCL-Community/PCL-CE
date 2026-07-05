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
            MinecraftLoaderKind.Fabric,
            MinecraftLoaderCardId.Fabric,
            "Fabric",
            "avares://PCL.Desktop/WpfOriginal/Images/Blocks/Fabric.png"),
        new(
            MinecraftLoaderKind.Quilt,
            MinecraftLoaderCardId.Quilt,
            "Quilt",
            "avares://PCL.Desktop/WpfOriginal/Images/Blocks/Quilt.png")
    ];

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
