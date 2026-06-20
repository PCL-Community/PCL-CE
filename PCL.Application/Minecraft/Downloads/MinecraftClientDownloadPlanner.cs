// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json.Nodes;
using PCL.Application.Minecraft.Assets;

namespace PCL.Application.Minecraft.Downloads;

public static class MinecraftClientDownloadPlanner
{
    private const long ClientJarMinimumSize = 1024L;

    public static MinecraftClientJarDownloadPlan CreateClientJarPlan(MinecraftClientJarDownloadPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.VersionJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstanceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionName);

        JsonObject? client = request.VersionJson["downloads"]?["client"] as JsonObject;
        string? url = client?["url"]?.ToString();
        if (string.IsNullOrWhiteSpace(url))
            return new MinecraftClientJarDownloadPlan(null, MinecraftClientDownloadFailureReason.NoClientJarDownloadInfo);

        return new MinecraftClientJarDownloadPlan(
            new MinecraftClientJarDownloadFile
            {
                Url = url,
                LocalPath = Path.Combine(request.InstanceDirectory, request.VersionName + ".jar"),
                MinimumSize = ClientJarMinimumSize,
                ActualSize = TryGetInt64(client?["size"]) ?? -1,
                Sha1 = client?["sha1"]?.ToString()
            },
            MinecraftClientDownloadFailureReason.None);
    }

    public static MinecraftAssetIndexDownloadPlan CreateAssetIndexPlan(MinecraftAssetIndexDownloadPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.VersionJson);
        ArgumentNullException.ThrowIfNull(request.InheritedVersionJsons);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftRootDirectory);

        MinecraftAssetIndexResolution resolution = MinecraftAssetIndexResolver.ResolveIndex(
            new MinecraftAssetIndexRequest
            {
                VersionJson = request.VersionJson,
                InheritedVersionJsons = request.InheritedVersionJsons,
                UseLegacyFallback = request.UseLegacyFallback,
                AllowUrlOnlyAssetIndex = request.AllowUrlOnlyAssetIndex
            });

        if (resolution.IndexJson is null)
            return new MinecraftAssetIndexDownloadPlan
            {
                UsedLegacyFallback = resolution.UsedLegacyFallback
            };

        string indexId = resolution.IndexJson["id"]?.ToString() ?? string.Empty;
        string? url = resolution.IndexJson["url"]?.ToString();
        return new MinecraftAssetIndexDownloadPlan
        {
            IndexId = indexId,
            Url = string.IsNullOrEmpty(url) ? null : url,
            LocalPath = Path.Combine(request.MinecraftRootDirectory, "assets", "indexes", indexId + ".json"),
            UsedLegacyFallback = resolution.UsedLegacyFallback
        };
    }

    private static long? TryGetInt64(JsonNode? node)
    {
        if (node is null)
            return null;
        return long.TryParse(node.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : null;
    }
}
