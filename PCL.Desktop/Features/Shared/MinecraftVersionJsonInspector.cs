// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Shared;

internal readonly record struct MinecraftVersionJsonInfo(
    string MinecraftVersionId,
    string? InheritsFrom,
    IReadOnlyList<string> Libraries);

internal static class MinecraftVersionJsonInspector
{
    public static MinecraftVersionJsonInfo Read(LaunchInstanceInfo instance)
    {
        if (!File.Exists(instance.VersionJsonPath))
            return new MinecraftVersionJsonInfo(instance.Name, null, []);

        try
        {
            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string inherited = ReadJsonString(root, "inheritsFrom");
            string clientVersion = ReadJsonString(root, "clientVersion");
            string id = ReadJsonString(root, "id");
            string minecraftVersionId = string.IsNullOrWhiteSpace(inherited)
                ? (!string.IsNullOrWhiteSpace(clientVersion)
                    ? clientVersion
                    : string.IsNullOrWhiteSpace(id) ? instance.Name : id)
                : inherited;

            return new MinecraftVersionJsonInfo(
                minecraftVersionId,
                string.IsNullOrWhiteSpace(inherited) ? null : inherited,
                ReadLibraryNames(root).ToArray());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new MinecraftVersionJsonInfo(instance.Name, null, []);
        }
    }

    private static string ReadJsonString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static IEnumerable<string> ReadLibraryNames(JsonElement root)
    {
        if (!root.TryGetProperty("libraries", out JsonElement libraries) ||
            libraries.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement library in libraries.EnumerateArray())
        {
            if (library.TryGetProperty("name", out JsonElement nameElement) &&
                nameElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                yield return nameElement.GetString()!;
            }
        }
    }
}

internal static class MinecraftLoaderLibraryDetector
{
    public static string? DetectVersion(IReadOnlyList<string> libraries, params string[] needles)
    {
        string? library = libraries.FirstOrDefault(library =>
            needles.Any(needle => library.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        return string.IsNullOrWhiteSpace(library) ? null : SimplifyVersion(library);
    }

    public static string SimplifyVersion(string library)
    {
        int versionIndex = library.LastIndexOf(':');
        if (versionIndex < 0 || versionIndex == library.Length - 1)
            return "已安装";

        string version = library[(versionIndex + 1)..];
        int minecraftPrefixIndex = version.IndexOf('-');
        return minecraftPrefixIndex > 0 && minecraftPrefixIndex < version.Length - 1
            ? version[(minecraftPrefixIndex + 1)..]
            : version;
    }
}
