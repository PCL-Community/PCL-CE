// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;

namespace PCL.Application.Minecraft.Assets;

public static class MinecraftAssetIndexResolver
{
    public const string LegacyIndexName = "legacy";
    public const string LegacyIndexSha1 = "c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729";
    public const int LegacyIndexSize = 134284;
    public const string LegacyIndexUrl =
        "https://launchermeta.mojang.com/mc-staging/assets/legacy/c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729/legacy.json";
    public const int LegacyIndexTotalSize = 111220701;

    public static MinecraftAssetIndexResolution ResolveIndex(MinecraftAssetIndexRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.VersionJson);
        ArgumentNullException.ThrowIfNull(request.InheritedVersionJsons);

        JsonObject? index = TryGetIndex(request.VersionJson, request.AllowUrlOnlyAssetIndex);
        if (index is not null)
            return new MinecraftAssetIndexResolution(index, UsedLegacyFallback: false);

        foreach (JsonObject inheritedVersionJson in request.InheritedVersionJsons)
        {
            index = TryGetIndex(inheritedVersionJson, request.AllowUrlOnlyAssetIndex);
            if (index is not null)
                return new MinecraftAssetIndexResolution(index, UsedLegacyFallback: false);
        }

        return request.UseLegacyFallback
            ? new MinecraftAssetIndexResolution(CreateLegacyIndex(), UsedLegacyFallback: true)
            : new MinecraftAssetIndexResolution(null, UsedLegacyFallback: false);
    }

    public static string GetIndexName(MinecraftAssetIndexNameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.VersionJson);
        ArgumentNullException.ThrowIfNull(request.InheritedVersionJsons);

        string? indexName = TryGetIndexName(request.VersionJson);
        if (indexName is not null)
            return indexName;

        foreach (JsonObject inheritedVersionJson in request.InheritedVersionJsons)
        {
            indexName = TryGetIndexName(inheritedVersionJson);
            if (indexName is not null)
                return indexName;
        }

        return LegacyIndexName;
    }

    private static JsonObject? TryGetIndex(JsonObject versionJson, bool allowUrlOnlyAssetIndex)
    {
        if (versionJson["assetIndex"] is not JsonObject assetIndex)
            return null;
        if (assetIndex["id"] is not null)
            return assetIndex;
        return allowUrlOnlyAssetIndex && assetIndex["url"] is not null ? assetIndex : null;
    }

    private static string? TryGetIndexName(JsonObject versionJson)
    {
        if (versionJson["assetIndex"] is JsonObject assetIndex &&
            assetIndex["id"] is JsonNode assetIndexId)
            return assetIndexId.ToString();

        return versionJson["assets"]?.ToString();
    }

    private static JsonObject CreateLegacyIndex() => new()
    {
        ["id"] = LegacyIndexName,
        ["sha1"] = LegacyIndexSha1,
        ["size"] = LegacyIndexSize,
        ["url"] = LegacyIndexUrl,
        ["totalSize"] = LegacyIndexTotalSize
    };
}
