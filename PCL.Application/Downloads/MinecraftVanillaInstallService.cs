// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Application.Minecraft.Assets;
using PCL.Application.Minecraft.Downloads;
using PCL.Application.Minecraft.Launch.Libraries;
using PCL.Core.IO.Download;
using PCL.Core.IO.Net;
using PCL.Core.Utils.Hash;

namespace PCL.Application.Downloads;

public sealed record MinecraftVersionManifestEntry(
    string Id,
    string Type,
    string Url,
    DateTimeOffset? ReleaseTime);

public sealed record MinecraftInstallRequest
{
    public required string VersionId { get; init; }
    public string? BaseVersionId { get; init; }
    public required string VersionJsonUrl { get; init; }
    public required string MinecraftRootDirectory { get; init; }
    public bool PreferOfficialSource { get; init; } = true;
    public int DownloadThreadLimit { get; init; } = 64;
    public MinecraftLoaderInstallRequest? Loader { get; init; }
    public bool ReplaceExistingVersion { get; init; }
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
    public int ActiveThreads { get; init; }
    public int ThreadLimit { get; init; } = 1;
    public IReadOnlyList<MinecraftInstallStepProgress> Steps { get; init; } = [];
}

public enum MinecraftInstallStepState
{
    Waiting,
    Running,
    Finished,
    Failed
}

public sealed record MinecraftInstallStepProgress(
    string Name,
    string Detail,
    double Progress,
    MinecraftInstallStepState State);

public sealed record MinecraftInstallResult(
    string VersionId,
    string MinecraftRootDirectory,
    string InstanceDirectory,
    string VersionJsonPath);

public sealed class MinecraftVanillaInstallService
{
    private const string VersionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private const int DefaultDownloadThreadLimit = 64;
    private const int MaxDownloadThreadLimit = 256;
    private readonly HttpClient _httpClient;
    private readonly IMinecraftLoaderMetadataService _loaderMetadataService;
    private readonly DownloadService _downloadService = new();

    public MinecraftVanillaInstallService(
        HttpClient? httpClient = null,
        IMinecraftLoaderMetadataService? loaderMetadataService = null)
    {
        _httpClient = httpClient ?? PortableHttp.Client;
        _loaderMetadataService = loaderMetadataService ?? new MinecraftLoaderMetadataService(_httpClient);
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
        string baseVersionId = string.IsNullOrWhiteSpace(request.BaseVersionId)
            ? request.VersionId
            : request.BaseVersionId.Trim();
        bool installsLoader = request.Loader is not null;
        if (installsLoader && string.Equals(request.VersionId, baseVersionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("加载器实例名称不能与原版版本名称相同。");
        }

        string vanillaInstallId = installsLoader ? baseVersionId : request.VersionId;
        string vanillaInstanceDirectory = Path.Combine(minecraftRoot, "versions", vanillaInstallId);
        string vanillaVersionJsonPath = Path.Combine(vanillaInstanceDirectory, vanillaInstallId + ".json");
        VersionCoreBackup? coreBackup = request.ReplaceExistingVersion
            ? VersionCoreBackup.Create(minecraftRoot, request.VersionId)
            : null;
        bool installCompleted = false;
        try
        {
            Directory.CreateDirectory(vanillaInstanceDirectory);
            int downloadThreadLimit = NormalizeDownloadThreadLimit(request.DownloadThreadLimit);

            progress?.Report(CreateProgress("准备安装", request.VersionId, 0d, 0, 1, 0, downloadThreadLimit));
            await DownloadIfNeededAsync(
                    MinecraftDownloadSourcePlanner.GetLauncherOrMetaSources(request.VersionJsonUrl, request.PreferOfficialSource),
                    vanillaVersionJsonPath,
                    expectedSize: -1,
                    expectedSha1: null,
                    "下载版本描述",
                    0,
                    1,
                    progress,
                    activeThreads: 1,
                    threadLimit: downloadThreadLimit,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            JsonObject versionJson = await ReadJsonObjectAsync(vanillaVersionJsonPath, cancellationToken).ConfigureAwait(false);
            await NormalizeVersionIdAsync(versionJson, vanillaInstallId, vanillaVersionJsonPath, cancellationToken).ConfigureAwait(false);
            await DownloadVersionFilesAsync(
                    vanillaInstallId,
                    versionJson,
                    minecraftRoot,
                    vanillaInstanceDirectory,
                    request.PreferOfficialSource,
                    downloadThreadLimit,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            MinecraftInstallResult result = request.Loader is { } loaderRequest
                ? await InstallLoaderAsync(
                        request,
                        loaderRequest,
                        baseVersionId,
                        versionJson,
                        minecraftRoot,
                        request.PreferOfficialSource,
                        downloadThreadLimit,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false)
                : new MinecraftInstallResult(
                    request.VersionId,
                    minecraftRoot,
                    vanillaInstanceDirectory,
                    vanillaVersionJsonPath);

            progress?.Report(CreateProgress("安装完成", request.VersionId, 1d, 1, 1, 0, downloadThreadLimit));
            installCompleted = true;
            return result;
        }
        finally
        {
            if (coreBackup is not null)
            {
                if (installCompleted)
                    coreBackup.Commit();
                else
                    coreBackup.Restore();
            }
        }
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
        int downloadThreadLimit = DefaultDownloadThreadLimit;
        progress?.Report(CreateProgress("准备修复", request.VersionId, 0d, 0, 1, 0, downloadThreadLimit));
        await DownloadVersionFilesAsync(
                request.VersionId,
                versionJson,
                minecraftRoot,
                instanceDirectory,
                request.PreferOfficialSource,
                downloadThreadLimit,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(CreateProgress("修复完成", request.VersionId, 1d, 1, 1, 0, downloadThreadLimit));
        return new MinecraftInstallResult(request.VersionId, minecraftRoot, instanceDirectory, request.VersionJsonPath);
    }

    private async Task DownloadVersionFilesAsync(
        string versionId,
        JsonObject versionJson,
        string minecraftRoot,
        string instanceDirectory,
        bool preferOfficialSource,
        int downloadThreadLimit,
        IProgress<MinecraftInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<PlannedDownload> files = [];
        AddClientJarDownload(files, versionId, versionJson, instanceDirectory, preferOfficialSource);
        AddLibraryDownloads(files, versionJson, minecraftRoot, instanceDirectory, preferOfficialSource);
        await AddAssetDownloadsAsync(
                files,
                versionJson,
                minecraftRoot,
                instanceDirectory,
                preferOfficialSource,
                downloadThreadLimit,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        int total = Math.Max(files.Count, 1);
        progress?.Report(CreateProgress("准备下载文件", $"{files.Count} 个文件", 0.02d, 0, total, 0, downloadThreadLimit));
        if (files.Count == 0)
        {
            progress?.Report(CreateProgress("文件检查完成", versionId, 1d, total, total, 0, downloadThreadLimit));
            return;
        }

        FileDownloadProgressReporter reporter = new(progress, files, downloadThreadLimit);
        ParallelOptions parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = downloadThreadLimit
        };
        await Parallel.ForEachAsync(
                files.Select(static (file, index) => new PlannedDownloadWorkItem(file, index)),
                parallelOptions,
                async (item, token) =>
                {
                    PlannedDownload file = item.File;
                    string fileName = Path.GetFileName(file.LocalPath);
                    reporter.WorkerStarted(item.Index, fileName);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        if (await IsExistingFileUsableAsync(
                                file.LocalPath,
                                file.ExpectedSize,
                                file.ExpectedSha1,
                                token).ConfigureAwait(false))
                        {
                            reporter.MarkComplete(item.Index, fileName);
                            return;
                        }

                        await DownloadIfNeededAsync(
                                file.Urls,
                                file.LocalPath,
                                file.ExpectedSize,
                                file.ExpectedSha1,
                                file.Stage,
                                0,
                                1,
                                new DelegateProgress<MinecraftInstallProgress>(
                                    update => reporter.ReportFileProgress(item.Index, update)),
                                activeThreads: 1,
                                threadLimit: 1,
                                cancellationToken: token)
                            .ConfigureAwait(false);
                        reporter.MarkComplete(item.Index, fileName);
                    }
                    finally
                    {
                        reporter.WorkerFinished(item.Index, fileName);
                    }
                })
            .ConfigureAwait(false);

        progress?.Report(CreateProgress("文件检查完成", versionId, 1d, total, total, 0, downloadThreadLimit));
    }

    private async Task<MinecraftInstallResult> InstallLoaderAsync(
        MinecraftInstallRequest request,
        MinecraftLoaderInstallRequest loaderRequest,
        string baseVersionId,
        JsonObject baseVersionJson,
        string minecraftRoot,
        bool preferOfficialSource,
        int downloadThreadLimit,
        IProgress<MinecraftInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (loaderRequest.Kind is MinecraftLoaderKind.LabyMod or MinecraftLoaderKind.LiteLoader)
        {
            return await InstallProfileLoaderAsync(
                    request,
                    loaderRequest,
                    baseVersionId,
                    minecraftRoot,
                    preferOfficialSource,
                    downloadThreadLimit,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        string loaderName = loaderRequest.Kind.ToString();
        progress?.Report(CreateProgress("准备安装加载器", $"{loaderName} {loaderRequest.LoaderVersion}", 0d, 0, 1, 0, downloadThreadLimit));

        MinecraftLoaderInstallMetadata metadata = await _loaderMetadataService.GetLoaderInstallMetadataAsync(
                loaderRequest,
                baseVersionId,
                cancellationToken)
            .ConfigureAwait(false);
        JsonObject loaderVersionJson = MinecraftLoaderVersionJsonBuilder.Create(
            new MinecraftLoaderVersionJsonRequest
            {
                VersionId = request.VersionId,
                MinecraftVersionId = baseVersionId,
                Loader = metadata,
                Type = baseVersionJson["type"]?.ToString() ?? "release",
                Time = TryReadDateTimeOffset(baseVersionJson["releaseTime"] ?? baseVersionJson["time"])
            });

        string instanceDirectory = Path.Combine(minecraftRoot, "versions", request.VersionId);
        string versionJsonPath = Path.Combine(instanceDirectory, request.VersionId + ".json");
        Directory.CreateDirectory(instanceDirectory);
        await WriteJsonObjectAsync(loaderVersionJson, versionJsonPath, cancellationToken).ConfigureAwait(false);

        await DownloadVersionFilesAsync(
                request.VersionId,
                loaderVersionJson,
                minecraftRoot,
                instanceDirectory,
                preferOfficialSource,
                downloadThreadLimit,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        return new MinecraftInstallResult(request.VersionId, minecraftRoot, instanceDirectory, versionJsonPath);
    }

    private async Task<MinecraftInstallResult> InstallProfileLoaderAsync(
        MinecraftInstallRequest request,
        MinecraftLoaderInstallRequest loaderRequest,
        string baseVersionId,
        string minecraftRoot,
        bool preferOfficialSource,
        int downloadThreadLimit,
        IProgress<MinecraftInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        string loaderName = loaderRequest.Kind.ToString();
        progress?.Report(CreateProgress(
            "获取加载器版本描述",
            $"{loaderName} {loaderRequest.LoaderVersion}",
            0d,
            0,
            1,
            0,
            downloadThreadLimit));

        JsonObject loaderVersionJson = await _loaderMetadataService.GetLoaderVersionProfileAsync(
                loaderRequest,
                baseVersionId,
                cancellationToken)
            .ConfigureAwait(false);
        loaderVersionJson["id"] = request.VersionId;
        if (loaderRequest.Kind == MinecraftLoaderKind.LabyMod)
            loaderVersionJson["clientVersion"] = baseVersionId;

        string instanceDirectory = Path.Combine(minecraftRoot, "versions", request.VersionId);
        string versionJsonPath = Path.Combine(instanceDirectory, request.VersionId + ".json");
        Directory.CreateDirectory(instanceDirectory);
        await WriteJsonObjectAsync(loaderVersionJson, versionJsonPath, cancellationToken).ConfigureAwait(false);

        await DownloadVersionFilesAsync(
                request.VersionId,
                loaderVersionJson,
                minecraftRoot,
                instanceDirectory,
                preferOfficialSource,
                downloadThreadLimit,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        return new MinecraftInstallResult(request.VersionId, minecraftRoot, instanceDirectory, versionJsonPath);
    }

    private async Task DownloadIfNeededAsync(
        IReadOnlyList<string> urls,
        string localPath,
        long expectedSize,
        string? expectedSha1,
        string stage,
        int completedFiles,
        int totalFiles,
        IProgress<MinecraftInstallProgress>? progress,
        int activeThreads,
        int threadLimit,
        CancellationToken cancellationToken)
    {
        if (await IsExistingFileUsableAsync(localPath, expectedSize, expectedSha1, cancellationToken).ConfigureAwait(false))
            return;

        ProgressThrottle progressThrottle = new();
        List<Exception> failures = [];
        foreach (string source in urls)
        {
            if (string.IsNullOrWhiteSpace(source))
                continue;

            DownloadTransferResult result = await _downloadService.DownloadAsync(
                new DownloadRequest
                {
                    Sources = [source],
                    DestinationPath = localPath,
                    ConnectionFactory = url => new HttpDlConnection(_httpClient, url, ConfigureRequest)
                },
                downloadProgress =>
                {
                    bool force = downloadProgress.Stage is
                        DownloadStage.Completed or
                        DownloadStage.Committing or
                        DownloadStage.Failed;
                    if (!progressThrottle.ShouldReport(force))
                        return;

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
                        SpeedBytesPerSecond = downloadProgress.BytesPerSecond,
                        ActiveThreads = activeThreads,
                        ThreadLimit = threadLimit,
                        Steps =
                        [
                            new MinecraftInstallStepProgress(
                                stage,
                                Path.GetFileName(localPath),
                                progressValue,
                                progressValue >= 1d ? MinecraftInstallStepState.Finished : MinecraftInstallStepState.Running)
                        ]
                    });
                },
                cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                failures.AddRange(result.Errors.Select(static error =>
                    error.Exception ?? new IOException(error.Message)));
                continue;
            }

            if (await IsExistingFileUsableAsync(localPath, expectedSize, expectedSha1, cancellationToken).ConfigureAwait(false))
                return;

            DeleteInvalidDownload(localPath);
            failures.Add(new IOException("文件校验失败：" + localPath));
        }

        throw new IOException("下载失败或文件校验失败：" + localPath, new AggregateException(failures));
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
            plan.File.Sha1,
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
            files.Add(new PlannedDownload(
                library.Urls,
                library.LocalPath,
                library.ActualSize,
                library.Sha1,
                "下载运行库"));
    }

    private async Task AddAssetDownloadsAsync(
        List<PlannedDownload> files,
        JsonObject versionJson,
        string minecraftRoot,
        string instanceDirectory,
        bool preferOfficialSource,
        int downloadThreadLimit,
        IProgress<MinecraftInstallProgress>? progress,
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
                expectedSha1: null,
                "下载资源索引",
                0,
                1,
                progress,
                activeThreads: 1,
                threadLimit: downloadThreadLimit,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(CreateProgress(
            "解析资源索引",
            Path.GetFileName(indexPlan.LocalPath!),
            0d,
            0,
            1,
            activeThreads: 1,
            threadLimit: downloadThreadLimit));
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
                asset.Hash,
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
        await WriteJsonObjectAsync(versionJson, versionJsonPath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteJsonObjectAsync(
        JsonObject versionJson,
        string versionJsonPath,
        CancellationToken cancellationToken)
    {
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

    private static async ValueTask<bool> IsExistingFileUsableAsync(
        string path,
        long expectedSize,
        string? expectedSha1,
        CancellationToken cancellationToken)
    {
        FileInfo file = new(path);
        if (!file.Exists)
            return false;
        if (expectedSize > 0 && file.Length != expectedSize)
            return false;
        if (string.IsNullOrWhiteSpace(expectedSha1))
            return true;

        return await IsFileSha1MatchAsync(file.FullName, expectedSha1, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> IsFileSha1MatchAsync(
        string path,
        string expectedSha1,
        CancellationToken cancellationToken)
    {
        string normalized = expectedSha1.Trim();
        if (normalized.Length != SHA1Provider.Instance.HashSizeInBytes * 2)
            return false;

        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        byte[] hash = await SHA1Provider.Instance.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).Equals(normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteInvalidDownload(string localPath)
    {
        try
        {
            File.Delete(localPath);
            File.Delete(localPath + ".PCLDownloading");
        }
        catch (IOException)
        {
            // The next install attempt will revalidate and retry the file.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep the original validation failure as the actionable error.
        }
    }

    private sealed class VersionCoreBackup
    {
        private readonly BackupEntry[] _entries;

        private VersionCoreBackup(BackupEntry[] entries)
        {
            _entries = entries;
        }

        public static VersionCoreBackup Create(string minecraftRoot, string versionId)
        {
            string instanceDirectory = Path.Combine(minecraftRoot, "versions", versionId);
            Directory.CreateDirectory(instanceDirectory);
            string backupSuffix = ".pcl-backup-" + Guid.NewGuid().ToString("N");
            BackupEntry[] entries =
            [
                CreateEntry(Path.Combine(instanceDirectory, versionId + ".json"), backupSuffix),
                CreateEntry(Path.Combine(instanceDirectory, versionId + ".jar"), backupSuffix)
            ];

            List<BackupEntry> moved = [];
            try
            {
                foreach (BackupEntry entry in entries)
                {
                    if (entry.BackupPath is null)
                        continue;

                    File.Move(entry.OriginalPath, entry.BackupPath);
                    moved.Add(entry);
                }
            }
            catch
            {
                foreach (BackupEntry entry in moved.AsEnumerable().Reverse())
                {
                    if (entry.BackupPath is not null && File.Exists(entry.BackupPath))
                        File.Move(entry.BackupPath, entry.OriginalPath, overwrite: true);
                }

                throw;
            }

            return new VersionCoreBackup(entries);
        }

        public void Commit()
        {
            foreach (BackupEntry entry in _entries)
            {
                try
                {
                    DeleteFileIfExists(entry.BackupPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The installed files are already committed. A stale backup is safer than failing the install.
                }
            }
        }

        public void Restore()
        {
            foreach (BackupEntry entry in _entries)
            {
                DeleteFileIfExists(entry.OriginalPath + ".PCLDownloading");
                DeleteFileIfExists(entry.OriginalPath);
                if (entry.BackupPath is not null && File.Exists(entry.BackupPath))
                    File.Move(entry.BackupPath, entry.OriginalPath, overwrite: true);
            }
        }

        private static BackupEntry CreateEntry(string originalPath, string backupSuffix) =>
            new(originalPath, File.Exists(originalPath) ? originalPath + backupSuffix : null);

        private static void DeleteFileIfExists(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }

        private sealed record BackupEntry(string OriginalPath, string? BackupPath);
    }

    private static MinecraftInstallProgress CreateProgress(
        string stage,
        string detail,
        double progress,
        int completed,
        int total,
        int activeThreads = 0,
        int threadLimit = 1,
        long speedBytesPerSecond = 0,
        long bytesReceived = 0,
        long totalBytes = -1,
        IReadOnlyList<MinecraftInstallStepProgress>? steps = null) =>
        new()
        {
            Stage = stage,
            Detail = detail,
            Progress = Math.Clamp(progress, 0d, 1d),
            CompletedFiles = completed,
            TotalFiles = total,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes,
            SpeedBytesPerSecond = speedBytesPerSecond,
            ActiveThreads = activeThreads,
            ThreadLimit = Math.Max(1, threadLimit),
            Steps = steps ?? CreateSingleStepProgress(stage, detail, progress)
        };

    private static IReadOnlyList<MinecraftInstallStepProgress> CreateSingleStepProgress(
        string stage,
        string detail,
        double progress)
    {
        if (string.IsNullOrWhiteSpace(stage))
            return [];

        return
        [
            new MinecraftInstallStepProgress(
                stage,
                detail,
                Math.Clamp(progress, 0d, 1d),
                progress >= 1d ? MinecraftInstallStepState.Finished : MinecraftInstallStepState.Running)
        ];
    }

    private static int NormalizeDownloadThreadLimit(int value) =>
        Math.Clamp(value <= 0 ? DefaultDownloadThreadLimit : value, 1, MaxDownloadThreadLimit);

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

    private static DateTimeOffset? TryReadDateTimeOffset(JsonNode? node)
    {
        string? text = node?.ToString();
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
        string? ExpectedSha1,
        string Stage);

    private sealed record PlannedDownloadWorkItem(PlannedDownload File, int Index);

    private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class ProgressThrottle
    {
        private static readonly long MinReportTicks = Stopwatch.Frequency / 10;
        private long _lastReportTimestamp;
        private bool _hasReported;

        public bool ShouldReport(bool force = false)
        {
            long now = Stopwatch.GetTimestamp();
            if (force || !_hasReported || now - _lastReportTimestamp >= MinReportTicks)
            {
                _hasReported = true;
                _lastReportTimestamp = now;
                return true;
            }

            return false;
        }
    }

    private sealed class FileDownloadProgressReporter
    {
        private const int MaxActiveFileRows = 8;

        private readonly object _sync = new();
        private readonly ProgressThrottle _throttle = new();
        private readonly IProgress<MinecraftInstallProgress>? _progress;
        private readonly double[] _fileProgress;
        private readonly long[] _fileSpeeds;
        private readonly bool[] _fileCompleted;
        private readonly bool[] _fileActive;
        private readonly string?[] _fileDetails;
        private readonly int[] _fileStageIndexes;
        private readonly DownloadStageAggregate[] _stages;
        private readonly int _threadLimit;
        private double _progressSum;
        private long _speedSum;
        private int _activeThreads;
        private int _completedFiles;

        public FileDownloadProgressReporter(
            IProgress<MinecraftInstallProgress>? progress,
            IReadOnlyList<PlannedDownload> files,
            int threadLimit)
        {
            _progress = progress;
            _fileProgress = new double[files.Count];
            _fileSpeeds = new long[files.Count];
            _fileCompleted = new bool[files.Count];
            _fileActive = new bool[files.Count];
            _fileDetails = new string?[files.Count];
            _fileStageIndexes = new int[files.Count];
            _stages = CreateStageAggregates(files, _fileStageIndexes);
            _threadLimit = Math.Max(1, threadLimit);
        }

        public void WorkerStarted(int index, string detail)
        {
            lock (_sync)
            {
                _activeThreads++;
                DownloadStageAggregate stage = StageFor(index);
                stage.ActiveFiles++;
                stage.Started = true;
                _fileActive[index] = true;
                _fileDetails[index] = detail;
                ReportLocked(stage.Name, detail);
            }
        }

        public void WorkerFinished(int index, string detail)
        {
            lock (_sync)
            {
                _activeThreads = Math.Max(0, _activeThreads - 1);
                DownloadStageAggregate stage = StageFor(index);
                stage.ActiveFiles = Math.Max(0, stage.ActiveFiles - 1);
                _fileActive[index] = false;
                ReportLocked(stage.Name, detail);
            }
        }

        public void ReportFileProgress(int index, MinecraftInstallProgress update)
        {
            lock (_sync)
            {
                DownloadStageAggregate stage = StageFor(index);
                stage.Started = true;
                double nextProgress = Math.Clamp(update.Progress, 0d, 1d);
                double progressDelta = nextProgress - _fileProgress[index];
                _fileProgress[index] = nextProgress;
                _progressSum += progressDelta;
                stage.ProgressSum += progressDelta;
                if (!string.IsNullOrWhiteSpace(update.Detail))
                    _fileDetails[index] = update.Detail;

                long nextSpeed = Math.Max(0, update.SpeedBytesPerSecond);
                _speedSum += nextSpeed - _fileSpeeds[index];
                _fileSpeeds[index] = nextSpeed;
                ReportLocked(update.Stage, update.Detail, update.BytesReceived, update.TotalBytes);
            }
        }

        public void MarkComplete(int index, string detail)
        {
            lock (_sync)
            {
                DownloadStageAggregate stage = StageFor(index);
                stage.Started = true;
                if (!_fileCompleted[index])
                {
                    _fileCompleted[index] = true;
                    _completedFiles++;
                    stage.CompletedFiles++;
                }

                _fileActive[index] = false;
                _fileDetails[index] = detail;
                double progressDelta = 1d - _fileProgress[index];
                _fileProgress[index] = 1d;
                _progressSum += progressDelta;
                stage.ProgressSum += progressDelta;
                _speedSum -= _fileSpeeds[index];
                _fileSpeeds[index] = 0;
                ReportLocked(stage.Name, detail, force: true);
            }
        }

        private void ReportLocked(
            string stage,
            string detail,
            long bytesReceived = 0,
            long totalBytes = -1,
            bool force = false)
        {
            if (!_throttle.ShouldReport(force))
                return;

            _progress?.Report(CreateProgress(
                stage,
                detail,
                _progressSum / _fileProgress.Length,
                _completedFiles,
                _fileProgress.Length,
                _activeThreads,
                _threadLimit,
                _speedSum,
                bytesReceived,
                totalBytes,
                CreateStepsLocked()));
        }

        private DownloadStageAggregate StageFor(int fileIndex) =>
            _stages[_fileStageIndexes[fileIndex]];

        private MinecraftInstallStepProgress[] CreateStepsLocked()
        {
            List<MinecraftInstallStepProgress> steps = new(_stages.Length + Math.Min(MaxActiveFileRows, _activeThreads));
            for (int i = 0; i < _stages.Length; i++)
            {
                DownloadStageAggregate stage = _stages[i];
                double progress = stage.TotalFiles <= 0
                    ? 1d
                    : Math.Clamp(stage.ProgressSum / stage.TotalFiles, 0d, 1d);
                MinecraftInstallStepState state = stage.CompletedFiles >= stage.TotalFiles
                    ? MinecraftInstallStepState.Finished
                    : stage.ActiveFiles > 0 || stage.Started || stage.ProgressSum > 0d
                        ? MinecraftInstallStepState.Running
                        : MinecraftInstallStepState.Waiting;
                steps.Add(new MinecraftInstallStepProgress(
                    stage.Name,
                    $"{stage.CompletedFiles} / {stage.TotalFiles} 个文件",
                    progress,
                    state));
            }

            int activeRows = 0;
            int hiddenActiveRows = 0;
            for (int i = 0; i < _fileActive.Length; i++)
            {
                if (!_fileActive[i] || _fileCompleted[i])
                    continue;

                if (activeRows >= MaxActiveFileRows)
                {
                    hiddenActiveRows++;
                    continue;
                }

                steps.Add(new MinecraftInstallStepProgress(
                    StageFor(i).Name,
                    _fileDetails[i] ?? "正在下载",
                    _fileProgress[i],
                    MinecraftInstallStepState.Running));
                activeRows++;
            }

            if (hiddenActiveRows > 0)
            {
                steps.Add(new MinecraftInstallStepProgress(
                    "其他下载线程",
                    $"还有 {hiddenActiveRows} 个文件正在下载",
                    _progressSum / _fileProgress.Length,
                    MinecraftInstallStepState.Running));
            }

            return steps.ToArray();
        }

        private static DownloadStageAggregate[] CreateStageAggregates(
            IReadOnlyList<PlannedDownload> files,
            int[] fileStageIndexes)
        {
            Dictionary<string, int> stageIndexes = new(StringComparer.Ordinal);
            List<DownloadStageAggregate> stages = [];
            for (int i = 0; i < files.Count; i++)
            {
                string stageName = files[i].Stage;
                if (!stageIndexes.TryGetValue(stageName, out int stageIndex))
                {
                    stageIndex = stages.Count;
                    stageIndexes.Add(stageName, stageIndex);
                    stages.Add(new DownloadStageAggregate(stageName));
                }

                fileStageIndexes[i] = stageIndex;
                stages[stageIndex].TotalFiles++;
            }

            return stages.ToArray();
        }
    }

    private sealed class DownloadStageAggregate(string name)
    {
        public string Name { get; } = name;
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public int ActiveFiles { get; set; }
        public double ProgressSum { get; set; }
        public bool Started { get; set; }
    }
}
