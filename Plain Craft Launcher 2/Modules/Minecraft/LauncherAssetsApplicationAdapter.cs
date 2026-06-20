extern alias PclApplication;

using System.IO;
using System.Text.Json.Nodes;
using AssetDownloadPlanRequest = PclApplication::PCL.Application.Minecraft.Downloads.MinecraftAssetDownloadPlanRequest;
using AssetDownloadPlanner = PclApplication::PCL.Application.Minecraft.Downloads.MinecraftAssetDownloadPlanner;
using AssetFileState = PclApplication::PCL.Application.Minecraft.Downloads.MinecraftAssetFileState;
using AssetListRequest = PclApplication::PCL.Application.Minecraft.Assets.MinecraftAssetListRequest;
using AssetListResolver = PclApplication::PCL.Application.Minecraft.Assets.MinecraftAssetListResolver;
using AssetToken = PclApplication::PCL.Application.Minecraft.Assets.MinecraftAssetToken;
using AssetIndexNameRequest = PclApplication::PCL.Application.Minecraft.Assets.MinecraftAssetIndexNameRequest;
using AssetIndexRequest = PclApplication::PCL.Application.Minecraft.Assets.MinecraftAssetIndexRequest;
using AssetIndexResolution = PclApplication::PCL.Application.Minecraft.Assets.MinecraftAssetIndexResolution;
using AssetIndexResolver = PclApplication::PCL.Application.Minecraft.Assets.MinecraftAssetIndexResolver;

namespace PCL;

internal static class LauncherAssetsApplicationAdapter
{
    public static string GetIndexName(McInstance instance)
    {
        return AssetIndexResolver.GetIndexName(new AssetIndexNameRequest
        {
            VersionJson = instance.JsonObject,
            InheritedVersionJsons = GetInheritedVersionJsons(instance)
        });
    }

    public static LauncherAssetIndexResolution ResolveIndex(
        McInstance instance,
        bool useLegacyFallback,
        bool allowUrlOnlyAssetIndex)
    {
        AssetIndexResolution resolution = AssetIndexResolver.ResolveIndex(new AssetIndexRequest
        {
            VersionJson = instance.JsonObject,
            InheritedVersionJsons = GetInheritedVersionJsons(instance),
            UseLegacyFallback = useLegacyFallback,
            AllowUrlOnlyAssetIndex = allowUrlOnlyAssetIndex
        });

        return new LauncherAssetIndexResolution(resolution.IndexJson, resolution.UsedLegacyFallback);
    }

    public static List<ModAssets.McAssetsToken> GetAssetList(McInstance instance, JsonObject indexJson)
    {
        var assets = AssetListResolver.GetAssetList(new AssetListRequest
        {
            IndexJson = indexJson,
            MinecraftRootDirectory = ModFolder.mcFolderSelected,
            InstanceDirectory = instance.PathIndie
        });

        return assets.Select(static asset => new ModAssets.McAssetsToken
        {
            localPath = asset.LocalPath,
            sourcePath = asset.SourcePath,
            hash = asset.Hash,
            size = asset.Size
        }).ToList();
    }

    public static string GetAssetHashPrefix(string hash) =>
        AssetListResolver.GetHashPrefix(hash);

    public static string GetAssetObjectUrl(string hash) =>
        AssetListResolver.GetObjectUrl(hash);

    public static LauncherAssetDownloadPlan CreateAssetDownloadPlan(
        IEnumerable<ModAssets.McAssetsToken> assets,
        bool checkHash,
        IReadOnlyDictionary<string, LauncherAssetFileState> existingFiles)
    {
        var plan = AssetDownloadPlanner.CreatePlan(new AssetDownloadPlanRequest
        {
            Assets = assets.Select(ToApplicationAsset).ToArray(),
            CheckHash = checkHash,
            ExistingFiles = existingFiles.ToDictionary(
                static pair => pair.Key,
                static pair => new AssetFileState(pair.Value.Exists, pair.Value.Length),
                StringComparer.Ordinal)
        });

        return new LauncherAssetDownloadPlan(
            plan.Files.Select(static file => new LauncherAssetDownloadFile(
                file.Url,
                file.LocalPath,
                file.Hash,
                file.ActualSize)).ToArray());
    }

    private static AssetToken ToApplicationAsset(ModAssets.McAssetsToken asset) => new()
    {
        LocalPath = asset.localPath,
        SourcePath = asset.sourcePath,
        Hash = asset.hash,
        Size = asset.size
    };

    private static IReadOnlyList<JsonObject> GetInheritedVersionJsons(McInstance instance)
    {
        List<JsonObject> inheritedVersionJsons = [];
        var currentInstance = instance;
        while (!string.IsNullOrEmpty(currentInstance.InheritInstanceName))
        {
            currentInstance = new McInstance(Path.Combine(ModFolder.mcFolderSelected, "versions", currentInstance.InheritInstanceName));
            inheritedVersionJsons.Add(currentInstance.JsonObject);
        }

        return inheritedVersionJsons;
    }
}

internal sealed record LauncherAssetIndexResolution(JsonObject? IndexJson, bool UsedLegacyFallback);

internal sealed record LauncherAssetFileState(bool Exists, long Length);

internal sealed record LauncherAssetDownloadPlan(IReadOnlyList<LauncherAssetDownloadFile> Files);

internal sealed record LauncherAssetDownloadFile(
    string Url,
    string LocalPath,
    string Hash,
    long ActualSize);
