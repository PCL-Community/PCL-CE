// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Text;
using Avalonia.Platform;

namespace PCL.Desktop.Controls.Legacy;

public static class SvgIconLoader
{
    public const string DefaultIconPack = "lucide";

    private static readonly ConcurrentDictionary<string, Lazy<SvgIconModel?>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    internal static SvgIconModel? Load(string? icon, string? defaultPack = null)
    {
        SvgIconKey? key = SvgIconKey.TryParse(icon, defaultPack ?? DefaultIconPack);
        if (key is null)
            return null;

        string cacheKey = key.Value.ToString();
        return Cache.GetOrAdd(cacheKey, _ => new Lazy<SvgIconModel?>(() => LoadCore(key.Value))).Value;
    }

    public static void ClearCache()
    {
        Cache.Clear();
    }

    private static SvgIconModel? LoadCore(SvgIconKey key)
    {
        try
        {
            Uri uri = new($"avares://PCL.Desktop/Assets/IconPacks/{key.Pack}/{key.Name}.svg", UriKind.Absolute);
            using Stream stream = AssetLoader.Open(uri);
            using StreamReader reader = new(stream, Encoding.UTF8, true);
            string svg = reader.ReadToEnd();
            return SvgIconParser.Parse(svg);
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct SvgIconKey(string Pack, string Name)
    {
        public static SvgIconKey? TryParse(string? icon, string defaultPack)
        {
            if (string.IsNullOrWhiteSpace(icon))
                return null;

            string normalized = icon.Trim().Replace('\\', '/');
            if (normalized.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^4];

            normalized = normalized.Trim('/');
            if (normalized.Length == 0 || normalized.Contains("..", StringComparison.Ordinal))
                return null;

            string[] parts = normalized.Split('/', 2,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string pack = parts.Length == 2 ? parts[0] : defaultPack;
            string name = parts.Length == 2 ? parts[1] : parts[0];

            name = NormalizeIconName(name);
            if (!IsSafeResourcePath(pack) || !IsSafeResourcePath(name))
                return null;

            return new SvgIconKey(pack, name);
        }

        public override string ToString() => $"{Pack}/{Name}";

        private static string NormalizeIconName(string name)
        {
            return name switch
            {
                "square-menu" => "menu-square",
                "help-circle" => "circle-help",
                "minus-square" => "square-minus",
                "plus-square" => "square-plus",
                _ => name
            };
        }

        private static bool IsSafeResourcePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('/') || value.EndsWith('/'))
                return false;

            return value.Split('/', StringSplitOptions.RemoveEmptyEntries).All(IsSafeResourceSegment);
        }

        private static bool IsSafeResourceSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value is "." or "..")
                return false;

            return value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.');
        }
    }
}
