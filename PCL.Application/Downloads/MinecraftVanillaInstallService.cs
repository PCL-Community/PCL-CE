// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Application.Minecraft.Assets;
using PCL.Application.Minecraft.Downloads;
using PCL.Application.Minecraft.Launch.Libraries;
using PCL.Core.IO.Download;
using PCL.Core.IO.Net;

namespace PCL.Application.Downloads;

public sealed record MinecraftVersionManifestEntry(
    string Id,
    string Type,
    string Url,
    DateTimeOffset? ReleaseTime);

public sealed record MinecraftInstallRequest
{
    public required string VersionId { get; init; }
    public required string VersionJsonUrl { get; init; }
    public required string MinecraftRootDirectory { get; init; }
    public bool PreferOfficialSource { get; init; } = true;
}

public sealed record MinecraftInstallProgress
{
    public required string Stage { get; init; }
    public string Detail { get; init; } = string.Empty;
    public double Progress { get; init; }
    public int CompletedFiles { get; init; }
    public int TotalFiles { get; init; }
    public long BytesReceived { get; init; }
    public long TotalBytes { get; init; } = -1;
    public long SpeedBytesPerSecond { get; init; }
}

public sealed record MinecraftInstallResult(
    string VersionId,
    string MinecraftRootDirectory,
    string InstanceDirectory,
    string VersionJsonPath);

public sealed class MinecraftVanillaInstallService
{
    private const string VersionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private readonly HttpClient _httpClient;
    private readonly DownloadService _downloadService = new();

    public MinecraftVanillaInstallService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? PortableHttp.Client;
    }

    public async Task<IReadOnlyList<MinecraftVersionManifestEntry>> GetVersionManifestAsync(
        bool preferOfficialSource = true,
        CancellationToken cancellationToken = default)
    {
        string manifestJson = await GetStringWithFailoverAsync(
                MinecraftDownloadSourcePlanner.GetLauncherOrMetaSources(VersionManifestUrl, preferOfficialSource),
                cancellationToken)
            .ConfigureAwait(false);

        using JsonDocument document = JsonDocument.Parse(manifestJson);
        if (!document.RootElement.TryGetProperty("versions", out JsonElement versions) ||
            versions.ValueKind != JsonValueKind.Array)
            return [];

        List<MinecraftVersionManifestEntry> result = [];
        foreach (JsonElement version in versions.EnumerateArray())
        {
            string? id = TryReadString(version, "id");
            string? type = TryReadString(version, "type");
            string? url = TryReadString(version, "url");
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(type) ||
                string.IsNullOrWhiteSpace(url))
                continue;

            result.Add(new MinecraftVersionManifestEntry(
                id,
                type,
                url,
                TryReadDate(version, "releaseTime")));
        }

        return result;
    }

    public async Task<MinecraftInstallResult> InstallAsync(
        MinecraftInstallRequest request,
        IProgress<MinecraftInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionJsonUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftRootDirectory);

        string minecraftRoot = Path.GetFullPath(request.MinecraftRootDirectory);
        string instanceDirectory = Path.Combine(minecraftRoot, "versions", request.VersionId);
        string versionJsonPath = Path.Combine(instanceDirectory, request.VersionId + ".json");
        Directory.CreateDirectory(instanceDirectory);

        progress?.Report(CreateProgress("准备安装", request.VersionId, 0d, 0, 1));
        await DownloadIfNeededAsync(
                MinecraftDownloadSourcePlanner.GetLauncherOrMetaSources(request.VersionJsonUrl, request.PreferOfficialSource),
                versionJsonPath,
                expectedSize: -1,
                "下载版本描述",
                0,
                1,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        JsonObject versionJson = await ReadJsonObjectAsync(versionJsonPath, cancellationToken).ConfigureAwait(false);
        await NormalizeVersionIdAsync(versionJson, request.VersionId, versionJsonPath, cancellationToken).ConfigureAwait(false);
        await DownloadVersionFilesAsync(
                request.VersionId,
                versionJson,
                minecraftRoot,
                instanceDirectory,
                request.PreferOfficialSource,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(CreateProgress("安装完成", request.VersionId, 1d, 1, 1));
        return new MinecraftInstallResult(request.VersionId, minecraftRoot, instanceDirectory, versionJsonPath);
    }

    public async Task<MinecraftInstallResult> RepairAsync(
        MinecraftRepairRequest request,
        IProgress<MinecraftInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionJsonPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstanceDirectory);

        string minecraftRoot = Path.GetFullPath(request.MinecraftRootDirectory);
        string instanceDirectory = Path.GetFullPath(request.InstanceDirectory);
        JsonObject versionJson = await ReadJsonObjectAsync(request.VersionJsonPath, cancellationToken).ConfigureAwait(false);
        progress?.Report(CreateProgress("准备修复", request.VersionId, 0d, 0, 1));
        await DownloadVersionFilesAsync(
                request.VersionId,
                versionJson,
                minecraftRoot,
                instanceDirectory,
                request.PreferOfficialSource,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(CreateProgress("修复完成", request.VersionId, 1d, 1, 1));
        return new MinecraftInstallResult(request.VersionId, minecraftRoot, instanceDirectory, request.VersionJsonPath);
    }

    private async Task DownloadVersionFilesAsync(
        string versionId,
        JsonObject versionJson,
        string minecraftRoot,
        string instanceDirectory,
        bool preferOfficialSource,
        IProgress<MinecraftInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<PlannedDownload> files = [];
        AddClientJarDownload(files, versionId, versionJson, instanceDirectory, preferOfficialSource);
        AddLibraryDownloads(files, versionJson, minecraftRoot, instanceDirectory, preferOfficialSource);
        await AddAssetDownloadsAsync(files, versionJson, minecraftRoot, instanceDirectory, preferOfficialSource, cancellationToken)
            .ConfigureAwait(false);

        int total = Math.Max(files.Count, 1);
        int completed = 0;
        progress?.Report(CreateProgress("准备下载文件", $"{files.Count} 个文件", 0.02d, 0, total));
        foreach (PlannedDownload file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsExistingFileUsable(file.LocalPath, file.ExpectedSize))
            {
                completed++;
                progress?.Report(CreateProgress("跳过已存在文件", Path.GetFileName(file.LocalPath), completed / (double)total, completed, total));
                continue;
            }

            await DownloadIfNeededAsync(
                    file.Urls,
                    file.LocalPath,
                    file.ExpectedSize,
                    file.Stage,
                    completed,
                    total,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
            completed++;
            progress?.Report(CreateProgress(file.Stage, Path.GetFileName(file.LocalPath), completed / (double)total, completed, total));
        }

        progress?.Report(CreateProgress("文件检查完成", versionId, 1d, total, total));
    }

    private async Task DownloadIfNeededAsync(
        IReadOnlyList<string> urls,
        string localPath,
        long expectedSize,
        string stage,
        int completedFiles,
        int totalFiles,
        IProgress<MinecraftInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (IsExistingFileUsable(localPath, expectedSize))
            return;

        DownloadTransferResult result = await _downloadService.DownloadAsync(
                new DownloadRequest
                {
                    Sources = urls,
                    DestinationPath = localPath,
                    ConnectionFactory = url => new HttpDlConnection(_httpClient, url, ConfigureRequest)
                },
                downloadProgress =>
                {
                    double fileRatio = downloadProgress.TotalBytes <= 0
                        ? 0d
                        : Math.Clamp(downloadProgress.DownloadedBytes / (double)downloadProgress.TotalBytes, 0d, 1d);
                    double progressValue = totalFiles <= 0
                        ? fileRatio
                        : Math.Clamp((completedFiles + fileRatio) / totalFiles, 0d, 1d);
                    progress?.Report(new MinecraftInstallProgress
                    {
                        Stage = stage,
                        Detail = Path.GetFileName(localPath),
                        Progress = progressValue,
                        CompletedFiles = completedFiles,
                        TotalFiles = totalFiles,
                        BytesReceived = downloadProgress.DownloadedBytes,
                        TotalBytes = downloadProgress.TotalBytes,
                        SpeedBytesPerSecond = downloadProgress.BytesPerSecond
                    });
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
            throw new IOException("下载失败：" + localPath);
    }

    private static void AddClientJarDownload(
        List<PlannedDownload> files,
        string versionId,
        JsonObject versionJson,
        string instanceDirectory,
        bool preferOfficialSource)
    {
        MinecraftClientJarDownloadPlan plan = MinecraftClientDownloadPlanner.CreateClientJarPlan(
            new MinecraftClientJarDownloadPlanRequest
            {
                VersionJson = versionJson,
                InstanceDirectory = instanceDirectory,
                VersionName = versionId
            });
        if (plan.File is null)
            return;

        files.Add(new PlannedDownload(
            MinecraftDownloadSourcePlanner.GetLauncherOrMetaSources(plan.File.Url, preferOfficialSource),
            plan.File.LocalPath,
            plan.File.ActualSize,
            "下载客户端"));
    }

    private static void AddLibraryDownloads(
        List<PlannedDownload> files,
        JsonObject versionJson,
        string minecraftRoot,
        string instanceDirectory,
        bool preferOfficialSource)
    {
        IReadOnlyList<MinecraftLibraryToken> libraries = MinecraftLibraryResolver.Resolve(
            new MinecraftLibraryResolutionRequest
            {
                VersionJson = versionJson,
                MinecraftRootDirectory = minecraftRoot,
                TargetInstanceDirectory = instanceDirectory,
                OperatingSystem = GetCurrentLibraryOperatingSystem(),
                Is64BitArchitecture = Environment.Is64BitOperatingSystem,
                OperatingSystemVersion = Environment.OSVersion.VersionString
            });
        MinecraftLibraryDownloadPlan plan = MinecraftLibraryDownloadPlanner.CreatePlan(
            new MinecraftLibraryDownloadPlanRequest
            {
                Libraries = libraries,
                MinecraftRootDirectory = minecraftRoot,
                PreferOfficialSource = preferOfficialSource
            });
        foreach (MinecraftLibraryDownloadFile library in plan.DownloadFiles)
            files.Add(new PlannedDownload(library.Urls, library.LocalPath, library.ActualSize, "下载运行库"));
    }

    private async Task AddAssetDownloadsAsync(
        List<PlannedDownload> files,
        JsonObject versionJson,
        string minecraftRoot,
        string instanceDirectory,
        bool preferOfficialSource,
        CancellationToken cancellationToken)
    {
        MinecraftAssetIndexDownloadPlan indexPlan = MinecraftClientDownloadPlanner.CreateAssetIndexPlan(
            new MinecraftAssetIndexDownloadPlanRequest
            {
                VersionJson = versionJson,
                MinecraftRootDirectory = minecraftRoot
            });
        if (!indexPlan.HasDownload)
            return;

        await DownloadIfNeededAsync(
                MinecraftDownloadSourcePlanner.GetLauncherOrMetaSources(indexPlan.Url!, preferOfficialSource),
                indexPlan.LocalPath!,
                expectedSize: -1,
                "下载资源索引",
                0,
                1,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);

        JsonObject indexJson = await ReadJsonObjectAsync(indexPlan.LocalPath!, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<MinecraftAssetToken> assets = MinecraftAssetListResolver.GetAssetList(
            new MinecraftAssetListRequest
            {
                IndexJson = indexJson,
                MinecraftRootDirectory = minecraftRoot,
                InstanceDirectory = instanceDirectory
            });
        Dictionary<string, MinecraftAssetFileState> existing = new(GetPathComparer());
        foreach (MinecraftAssetToken asset in assets)
        {
            FileInfo file = new(asset.LocalPath);
            existing[asset.LocalPath] = new MinecraftAssetFileState(file.Exists, file.Exists ? file.Length : 0L);
        }

        MinecraftAssetDownloadPlan plan = MinecraftAssetDownloadPlanner.CreatePlan(
            new MinecraftAssetDownloadPlanRequest
            {
                Assets = assets,
                ExistingFiles = existing
            });
        foreach (MinecraftAssetDownloadFile asset in plan.Files)
        {
            files.Add(new PlannedDownload(
                MinecraftDownloadSourcePlanner.GetAssetSources(asset.Url, preferOfficialSource),
                asset.LocalPath,
                asset.ActualSize,
                "下载资源文件"));
        }
    }

    private async Task<string> GetStringWithFailoverAsync(
        IReadOnlyList<string> urls,
        CancellationToken cancellationToken)
    {
        List<Exception> errors = [];
        foreach (string url in urls)
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                ConfigureRequest(request);
                using HttpResponseMessage response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                errors.Add(ex);
            }
        }

        throw new HttpRequestException("无法获取 Minecraft 版本清单。", new AggregateException(errors));
    }

    private static async Task<JsonObject> ReadJsonObjectAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 64 * 1024,
            useAsync: true);
        JsonNode? node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return node as JsonObject
               ?? throw new FormatException("JSON 根节点不是对象：" + path);
    }

    private static async Task NormalizeVersionIdAsync(
        JsonObject versionJson,
        string versionId,
        string versionJsonPath,
        CancellationToken cancellationToken)
    {
        if (string.Equals(versionJson["id"]?.ToString(), versionId, StringComparison.Ordinal))
            return;

        versionJson["id"] = versionId;
        string tempPath = versionJsonPath + ".tmp";
        await using (FileStream stream = new(
                         tempPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.Read,
                         bufferSize: 16 * 1024,
                         useAsync: true))
        {
            using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });
            versionJson.WriteTo(writer);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, versionJsonPath, overwrite: true);
    }

    private static bool IsExistingFileUsable(string path, long expectedSize)
    {
        FileInfo file = new(path);
        if (!file.Exists)
            return false;
        return expectedSize <= 0 || file.Length == expectedSize;
    }

    private static MinecraftInstallProgress CreateProgress(
        string stage,
        string detail,
        double progress,
        int completed,
        int total) =>
        new()
        {
            Stage = stage,
            Detail = detail,
            Progress = Math.Clamp(progress, 0d, 1d),
            CompletedFiles = completed,
            TotalFiles = total
        };

    private static string? TryReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateTimeOffset? TryReadDate(JsonElement element, string propertyName)
    {
        string? text = TryReadString(element, propertyName);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset value)
            ? value
            : null;
    }

    private static MinecraftLibraryOperatingSystem GetCurrentLibraryOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
            return MinecraftLibraryOperatingSystem.Win32;
        if (OperatingSystem.IsLinux())
            return MinecraftLibraryOperatingSystem.Linux;
        if (OperatingSystem.IsMacOS())
            return MinecraftLibraryOperatingSystem.MacOs;
        return MinecraftLibraryOperatingSystem.Unknown;
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static void ConfigureRequest(HttpRequestMessage request)
    {
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
        string language = CultureInfo.CurrentUICulture.Name;
        request.Headers.AcceptLanguage.ParseAdd(string.IsNullOrWhiteSpace(language) ? "zh-CN" : language);
    }

    private sealed record PlannedDownload(
        IReadOnlyList<string> Urls,
        string LocalPath,
        long ExpectedSize,
        string Stage);
}
