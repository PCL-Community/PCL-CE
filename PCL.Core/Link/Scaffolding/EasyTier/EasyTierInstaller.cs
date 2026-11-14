using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;

namespace PCL.Core.Link.Scaffolding.EasyTier;

/// <summary>
///     EasyTier 依赖包的下载与安装。
///     平台差异由 <see cref="EasyTierPlatform" /> 提供，本类不判断操作系统。
/// </summary>
public static class EasyTierInstaller
{
    private static readonly SemaphoreSlim _InstallGate = new(1, 1);
    private static readonly TimeSpan _DownloadTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     按优先级返回依赖包下载地址。
    /// </summary>
    public static IReadOnlyList<string> GetDownloadUrls(string version, EasyTierPackage package)
    {
        var archiveName = package.GetArchiveName(version);
        return
        [
            $"https://staticassets.naids.com/resources/pclce/static/easytier/{archiveName}",
            $"https://s3.pysio.online/pcl2-ce/static/easytier/{archiveName}",
            $"https://github.com/EasyTier/EasyTier/releases/download/v{version}/{archiveName}"
        ];
    }

    /// <summary>
    ///     确保当前平台的 EasyTier 依赖已经安装；已安装时不会重复下载。
    /// </summary>
    /// <returns>安装完成且必需文件齐全时返回 <c>true</c>。</returns>
    public static async Task<bool> EnsureInstalledAsync(
        CancellationToken cancellationToken = default)
    {
        if (EasyTierMetadata.IsInstalled)
        {
            EasyTierMetadata.EnsureExecutable();
            return true;
        }

        await _InstallGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (EasyTierMetadata.IsInstalled)
            {
                EasyTierMetadata.EnsureExecutable();
                return true;
            }

            const string version = EasyTierMetadata.CurrentEasyTierVer;
            var package = EasyTierMetadata.Package;
            var versionRoot = Path.Combine(Paths.SharedLocalData, "EasyTier", version);
            Directory.CreateDirectory(versionRoot);

            var archivePath = Path.Combine(versionRoot, $".download-{Guid.NewGuid():N}.zip");
            try
            {
                await _DownloadAsync(package, version, archivePath, cancellationToken)
                    .ConfigureAwait(false);
                return await InstallArchiveAsync(archivePath, version, package, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _TryDeleteFile(archivePath);
            }
        }
        finally
        {
            _InstallGate.Release();
        }
    }

    /// <summary>
    ///     从本地压缩包安装指定平台的 EasyTier 依赖。
    ///     先解压到同卷 staging 目录，校验必需文件后再移动到最终目录，避免半成品被误判为已安装。
    /// </summary>
    internal static async Task<bool> InstallArchiveAsync(
        string archivePath,
        string version,
        EasyTierPackage package,
        CancellationToken cancellationToken = default)
    {
        var versionRoot = Path.Combine(Paths.SharedLocalData, "EasyTier", version);
        Directory.CreateDirectory(versionRoot);

        var stagingRoot = Path.Combine(versionRoot, $".staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        try
        {
            var extractRoot = Path.Combine(stagingRoot, "extract");
            await Files
                .ExtractFileAsync(
                    archivePath,
                    extractRoot,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var extractedRoot = Path.Combine(extractRoot, package.FolderName);
            if (!_RequiredFilesExist(extractedRoot, package))
                extractedRoot = extractRoot; // 兼容压缩包没有顶层目录的情况

            if (!_RequiredFilesExist(extractedRoot, package))
                throw new InvalidDataException(
                    $"EasyTier 依赖包不完整：{package.GetArchiveName(version)}");

            var finalRoot = Path.Combine(versionRoot, package.FolderName);
            if (Directory.Exists(finalRoot))
                Directory.Delete(finalRoot, true);

            Directory.Move(extractedRoot, finalRoot);
            EasyTierMetadata.EnsureExecutable(package, finalRoot);
            _CleanupOldVersions(version);

            return _RequiredFilesExist(finalRoot, package);
        }
        finally
        {
            _TryDeleteDirectory(stagingRoot);
        }
    }

    private static async Task _DownloadAsync(
        EasyTierPackage package,
        string version,
        string archivePath,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        foreach (var url in GetDownloadUrls(version, package))
            try
            {
                LogWrapper.Info("EasyTier", $"下载依赖包：{url}");

                using var timeoutCts =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_DownloadTimeout);

                using var response = await HttpRequest
                    .Create(url)
                    .SendAsync(
                        httpCompletionOption: HttpCompletionOption.ResponseHeadersRead,
                        retryTimes: 1,
                        cancellationToken: timeoutCts.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var source =
                    await response.AsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
                await using var target = new FileStream(
                    archivePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true);
                await source.CopyToAsync(target, timeoutCts.Token).ConfigureAwait(false);

                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException
                                       || !cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
                LogWrapper.Warn(ex, "EasyTier", $"从 {url} 下载 EasyTier 依赖包失败");
                _TryDeleteFile(archivePath);
            }

        throw new IOException("下载 EasyTier 依赖文件失败", lastError);
    }

    private static bool _RequiredFilesExist(string root, EasyTierPackage package)
    {
        return package.RequiredFiles.All(fileName => File.Exists(Path.Combine(root, fileName)));
    }

    private static void _CleanupOldVersions(string currentVersion)
    {
        var root = Path.Combine(Paths.SharedLocalData, "EasyTier");
        if (!Directory.Exists(root))
            return;

        foreach (var folder in Directory.GetDirectories(root))
        {
            if (string.Equals(Path.GetFileName(folder), currentVersion, StringComparison.Ordinal))
                continue;

            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "EasyTier", $"清理旧版本 EasyTier 失败：{folder}");
            }
        }
    }

    private static void _TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "EasyTier", $"清理 EasyTier 临时目录失败：{path}");
        }
    }

    private static void _TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "EasyTier", $"清理 EasyTier 临时文件失败：{path}");
        }
    }
}