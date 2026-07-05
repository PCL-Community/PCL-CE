// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json.Nodes;

namespace PCL.Application.Downloads;

public sealed record MinecraftLoaderVersionJsonRequest
{
    public required string VersionId { get; init; }
    public required string MinecraftVersionId { get; init; }
    public required MinecraftLoaderInstallMetadata Loader { get; init; }
    public string Type { get; init; } = "release";
    public DateTimeOffset? Time { get; init; }
}

public static class MinecraftLoaderVersionJsonBuilder
{
    public static JsonObject Create(MinecraftLoaderVersionJsonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftVersionId);
        ArgumentNullException.ThrowIfNull(request.Loader);

        string time = (request.Time ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        JsonObject versionJson = new()
        {
            ["id"] = request.VersionId,
            ["inheritsFrom"] = request.MinecraftVersionId,
            ["releaseTime"] = time,
            ["time"] = time,
            ["type"] = string.IsNullOrWhiteSpace(request.Type) ? "release" : request.Type,
            ["mainClass"] = request.Loader.MainClass,
            ["libraries"] = CreateLibraries(request.Loader)
        };

        if (request.Loader.MinimumJavaVersion is int minimumJavaVersion)
        {
            versionJson["javaVersion"] = new JsonObject
            {
                ["component"] = "java-runtime-gamma",
                ["majorVersion"] = minimumJavaVersion
            };
        }

        return versionJson;
    }

    public static string CreateDefaultVersionId(MinecraftLoaderKind kind, string gameVersion, string loaderVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(loaderVersion);

        string loaderName = kind switch
        {
            MinecraftLoaderKind.Fabric => "fabric-loader",
            MinecraftLoaderKind.Quilt => "quilt-loader",
            _ => kind.ToString().ToLowerInvariant()
        };
        return $"{loaderName}-{loaderVersion}-{gameVersion}";
    }

    private static JsonArray CreateLibraries(MinecraftLoaderInstallMetadata loader)
    {
        JsonArray libraries = [];
        foreach (MinecraftLoaderLibrary library in loader.Libraries)
        {
            JsonObject libraryJson = new()
            {
                ["name"] = library.Name
            };
            if (!string.IsNullOrWhiteSpace(library.Url))
                libraryJson["url"] = library.Url;

            libraries.Add((JsonNode)libraryJson);
        }

        return libraries;
    }
}
