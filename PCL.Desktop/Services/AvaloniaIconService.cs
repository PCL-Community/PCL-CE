// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Frozen;
using Avalonia.Platform;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Services;

public sealed class AvaloniaIconService : IIconService
{
    public static AvaloniaIconService Shared { get; } = new();

    private static readonly FrozenDictionary<string, IconResource> Icons =
        new[]
            {
                "bell",
                "boxes",
                "chevron-down",
                "circle-check",
                "circle-x",
                "copy",
                "download",
                "eraser",
                "file-text",
                "home",
                "info",
                "monitor",
                "moon",
                "minus",
                "package",
                "palette",
                "play",
                "save",
                "settings",
                "sun",
                "terminal",
                "trash-2",
                "triangle-alert",
                "users",
                "x"
            }
            .ToFrozenDictionary(
                static name => $"lucide/{name}",
                static name =>
                    new IconResource(
                        $"lucide/{name}",
                        new Uri(
                            $"avares://PCL.Desktop/Assets/IconPacks/lucide/{name}.svg")));

    public IconResource? GetIcon(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string normalized = key.StartsWith(
            "lucide/",
            StringComparison.Ordinal)
            ? key
            : $"lucide/{key}";
        return Icons.GetValueOrDefault(normalized);
    }

    internal static bool ValidateResources(IAssetLoader assetLoader)
    {
        ArgumentNullException.ThrowIfNull(assetLoader);
        return assetLoader.Exists(
            new Uri("avares://PCL.Desktop/Assets/icon.png")) &&
        Icons.Values.All(
            icon => assetLoader.Exists(icon.ResourceUri));
    }
}
