// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json.Nodes;

namespace PCL.Application.Minecraft.Assets;

public static class MinecraftAssetListResolver
{
    public static IReadOnlyList<MinecraftAssetToken> GetAssetList(MinecraftAssetListRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.IndexJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstanceDirectory);

        JsonObject objects = request.IndexJson["objects"]?.AsObject()
                             ?? throw new FormatException("Asset index does not contain an objects map.");
        bool mapToResources = request.IndexJson["map_to_resources"]?.GetValue<bool>() == true;
        bool virtualAssets = request.IndexJson["virtual"]?.GetValue<bool>() == true;

        List<MinecraftAssetToken> result = new(objects.Count);
        foreach (KeyValuePair<string, JsonNode?> asset in objects)
        {
            JsonNode node = asset.Value ?? throw new FormatException($"Asset '{asset.Key}' is null.");
            string hash = node["hash"]?.ToString()
                          ?? throw new FormatException($"Asset '{asset.Key}' does not contain a hash.");
            long size = ParseSize(node, asset.Key);
            string localPath = GetLocalPath(request, asset.Key, hash, mapToResources, virtualAssets);
            result.Add(new MinecraftAssetToken
            {
                LocalPath = localPath,
                SourcePath = asset.Key,
                Hash = hash,
                Size = size
            });
        }

        return result;
    }

    public static string GetHashPrefix(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        if (hash.Length < 2)
            throw new ArgumentException("Asset hash must contain at least two characters.", nameof(hash));

        return hash[..2];
    }

    public static string GetObjectUrl(string hash) =>
        $"https://resources.download.minecraft.net/{GetHashPrefix(hash)}/{hash}";

    private static string GetLocalPath(
        MinecraftAssetListRequest request,
        string sourcePath,
        string hash,
        bool mapToResources,
        bool virtualAssets)
    {
        if (mapToResources)
            return Path.Combine(request.InstanceDirectory, "resources", ToNativeRelativePath(sourcePath));
        if (virtualAssets)
            return Path.Combine(request.MinecraftRootDirectory, "assets", "virtual", "legacy", ToNativeRelativePath(sourcePath));

        return Path.Combine(request.MinecraftRootDirectory, "assets", "objects", GetHashPrefix(hash), hash);
    }

    private static long ParseSize(JsonNode assetNode, string sourcePath)
    {
        string? sizeText = assetNode["size"]?.ToString();
        if (long.TryParse(sizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long size))
            return size;

        throw new FormatException($"Asset '{sourcePath}' does not contain a valid size.");
    }

    private static string ToNativeRelativePath(string sourcePath) =>
        sourcePath.Replace('/', Path.DirectorySeparatorChar);
}
