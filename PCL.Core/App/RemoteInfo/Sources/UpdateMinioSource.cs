using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Net.Downloader;
using PCL.Core.Net.Http.Client;
using PCL.Core.Utils.Diff;

namespace PCL.Core.App.RemoteInfo.Sources;

public class UpdateMinioSource(string baseUrl, string name = "Minio") : IUpdateSource
{
    #region Data Models

    private sealed record VersionAssetsDataModel(
        [property: JsonPropertyName("assets")] VersionDataModel[] Assets
    );

    private sealed record VersionInfoDataModel(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("code")] int Code
    );

    private sealed record VersionDataModel(
        [property: JsonPropertyName("version")] VersionInfoDataModel Version,
        [property: JsonPropertyName("sha256")] string Sha256,
        [property: JsonPropertyName("changelog")] string ChangeLog,
        [property: JsonPropertyName("patches")] string[] Patches,
        [property: JsonPropertyName("downloads")] string[] Downloads
    );

    #endregion
    
    public bool IsAvailable => !string.IsNullOrWhiteSpace(baseUrl);

    public string SourceName => name;

    private static readonly string _TempPath = Path.Combine(FileService.TempPath, "Cache", "Update");
    
    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">当版本信息为 null 时抛出</exception>
    /// <exception cref="HttpRequestException">当获取版本信息失败时抛出</exception>
    public async Task<VersionData> GetLatestVersionAsync()
    {
        var ret = await _GetVersionDataModelAsync();
        return new VersionData(
            ret.Version.Code,
            ret.Version.Name,
            ret.ChangeLog,
            ret.Sha256);
    }
    
    private async Task<VersionDataModel> _GetVersionDataModelAsync()
    {
        VersionDataModel? ret;
        try
        {
            _LogTrace("开始获取版本信息");
            var channelName = Config.System.Update.UpdateChannel switch
            {
                0 => "sr",
                1 => "fr",
                _ => "sr"
            } + (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64");
            
            var assets = await _GetRemoteInfoByNameAsync<VersionAssetsDataModel>(
                $"updates-{channelName}", "updates/").ConfigureAwait(false);
            _LogTrace("版本信息获取完成");

            ret = assets?.Assets.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("从远程获取版本信息失败", ex);
        }

        return ret ?? throw new InvalidDataException("未找到远程版本信息");
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">当公告信息为 null 时抛出</exception>
    /// <exception cref="HttpRequestException">当获取公告信息失败时抛出</exception>
    public async Task<AnnouncementsListModel> GetAnnouncementAsync()
    {
        AnnouncementsListModel? ret;
        try
        {
            _LogTrace("开始获取公告信息");
            ret = await _GetRemoteInfoByNameAsync<AnnouncementsListModel>("announcement")
                .ConfigureAwait(false);
            _LogTrace("公告信息获取完成");
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("从远程获取公告信息失败", ex);
        }

        return ret ?? throw new InvalidDataException("未找到远程公告信息");
    }

    #region Download Workflow

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">当版本信息未缓存时抛出</exception>
    public async Task<bool> DownloadAsync(string outputPath)
    {
        _LogInfo("准备下载更新文件");

        var tempDownloadDir = _PrepareTempDirectory();

        var (task, isPatch) = await _CreateDownloadTaskAsync(
            await _GetVersionDataModelAsync(), 
            tempDownloadDir).ConfigureAwait(false);

        _LogInfo("开始下载更新文件");
        var manager = new DownloadManager(new FastMirrorSelector(new HttpClient()));
        await manager.DownloadAsync(task, CancellationToken.None).ConfigureAwait(false);

        _LogInfo("下载完成，准备使用更新文件");
        await _UseUpdateFileAsync(task.TargetPath, outputPath, isPatch).ConfigureAwait(false);
        _LogInfo("更新文件处理完成");
        return true;
    }

    private async Task<(DownloadTask task, bool isPatch)> _CreateDownloadTaskAsync(
        VersionDataModel versionJson,
        string tempDir)
    {
        var updateSha256 = versionJson.Sha256;
        var selfSha256 = await Files.GetFileSHA256Async(Basics.ExecutablePath).ConfigureAwait(false);

        var patchFileName = $"{selfSha256}_{updateSha256}.patch";
        var patches = versionJson.Patches;

        if (patches.Contains(patchFileName))
        {
            _LogInfo("发现可用的差分更新，准备使用差分更新");
            var tempPath = Path.Combine(tempDir, patchFileName);

            return (new DownloadTask(
                new Uri($"{baseUrl}static/patch/{patchFileName}"),
                tempPath
            ), true);
        }

        _LogInfo("未发现可用的差分更新，准备使用完整更新包");

        var downloads = versionJson.Downloads;
        if (downloads is null || downloads.Length == 0)
        {
            throw new InvalidDataException("未找到可用的下载链接");
        }

        return (new DownloadTask(
                downloads.Select(download => new Uri(download)),
                Path.Combine(tempDir, $"{updateSha256}.zip")),
            false);
    }

    private static async Task _UseUpdateFileAsync(string updateFilePath, string outputPath, bool isPatch)
    {
        if (isPatch)
        {
            var diff = new BsDiff();
            var baseBytes = await Files.ReadAllBytesOrEmptyAsync(Basics.ExecutablePath).ConfigureAwait(false);
            var patchBytes = await Files.ReadAllBytesOrEmptyAsync(updateFilePath).ConfigureAwait(false);
            var newFile = await diff.ApplyAsync(baseBytes, patchBytes).ConfigureAwait(false);
            await Files.WriteFileAsync(outputPath, newFile).ConfigureAwait(false);
            return;
        }

        await using var fs = new FileStream(updateFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

        var entry = _FindExecutableEntry(zip);
        if (entry is null)
            throw new InvalidDataException("更新包中未找到可执行文件");

        entry.ExtractToFile(outputPath, overwrite: true); 
    }


    private static ZipArchiveEntry? _FindExecutableEntry(ZipArchive zip) =>
        zip.Entries.FirstOrDefault(e =>
            e.Name.Contains("Plain Craft Launcher Community Edition.exe", StringComparison.OrdinalIgnoreCase)) ??
        zip.Entries.FirstOrDefault(e =>
            e.Name.Contains("Plain Craft Launcher", StringComparison.OrdinalIgnoreCase)) ??
        zip.Entries.FirstOrDefault(e =>
            e.Name.Contains("Launcher", StringComparison.OrdinalIgnoreCase)) ??
        zip.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

    private static string _PrepareTempDirectory()
    {
        var path = Path.Combine(_TempPath, "Download");
        Directory.CreateDirectory(path);

        return path;
    }

    #endregion

    /// <summary>
    /// 通过名称获取远程信息
    /// </summary>
    /// <param name="path">保存路径</param>
    /// <param name="versionName">名称</param>
    /// <returns>远程信息</returns>
    private async Task<T?> _GetRemoteInfoByNameAsync<T>(string versionName, string path = "")
    {
        _LogTrace("拉取远程信息...");

        var builder = HttpRequestBuilder.Create($"{baseUrl}apiv2/{path}{versionName}.json", HttpMethod.Get);
        var result = await builder.SendAsync().ConfigureAwait(false);
        var remoteJson = await result.AsJsonAsync<T>().ConfigureAwait(false);

        _LogTrace("远程信息拉取完成");

        return remoteJson;
    }

    #region Logger Wrapper

    private void _LogInfo(string msg)
    {
        LogWrapper.Info("Update", msg);
    }

    private void _LogWarning(string msg, Exception? ex = null)
    {
        LogWrapper.Warn(ex, "Update", msg);
    }

    private void _LogError(string msg, Exception? ex = null)
    {
        LogWrapper.Error(ex, "Update", msg);
    }

    private void _LogTrace(string msg)
    {
        LogWrapper.Trace("Update", msg);
    }

    #endregion
}
