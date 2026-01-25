using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Net.Downloader;

/// <summary>
/// 下载任务
/// </summary>
public class DownloadTask
{
    public List<Uri> Mirrors { get; }
    public string TargetPath { get; }
    public List<DownloadSegment> Segments { get; } = [];
    public Uri ActiveUri { get; private set; }
    public long TotalSize { get; private set; }
    public bool UseBestMirror { get; set; } = true;
    public bool SupportsRange { get; internal set; } = true;
    
    /// <summary>
    /// 创建下载任务
    /// </summary>
    /// <param name="mirror">下载链接</param>
    /// <param name="targetPath">目标路径</param>
    public DownloadTask(Uri mirror, string targetPath) : this([mirror], targetPath) { }

    /// <summary>
    /// 创建下载任务
    /// </summary>
    /// <param name="mirrors">下载链接列表</param>
    /// <param name="targetPath">目标路径</param>
    public DownloadTask(IEnumerable<Uri> mirrors, string targetPath)
    {
        Mirrors = mirrors.ToList();
        TargetPath = targetPath;
        ActiveUri = Mirrors[0];
    }

    /// <summary>
    /// 切换到下一个镜像
    /// </summary>
    public void RotateMirror()
    {
        if (Mirrors.Count < 1)
        {
            LogWrapper.Warn("Downloader", "镜像列表为空，无法切换镜像");
            return;
        }

        lock (Mirrors)
        {
            var currentIndex = Mirrors.IndexOf(ActiveUri);
            var nextIndex = (currentIndex + 1) % Mirrors.Count;
            var oldUri = ActiveUri;
            ActiveUri = Mirrors[nextIndex];
            LogWrapper.Debug("Downloader", $"切换镜像: {oldUri} -> {ActiveUri}");
        }
    }

    /// <summary>
    /// 准备下载任务
    /// </summary>
    /// <param name="client">HTTP 客户端</param>
    /// <param name="selector">镜像选择器</param>
    /// <param name="token">取消令牌</param>
    public async Task PrepareAsync(HttpClient client, IMirrorSelector selector, CancellationToken token)
    {
        LogWrapper.Debug("Downloader", "开始准备下载任务");

        if (UseBestMirror && Mirrors.Count > 1)
        {
            LogWrapper.Debug("Downloader", $"正在选择最佳镜像，共有 {Mirrors.Count} 个镜像");
            var bestMirror = await selector.GetBestMirrorAsync(Mirrors, token).ConfigureAwait(false);
            LogWrapper.Debug("Downloader", $"选择的最佳镜像: {bestMirror}");
            ActiveUri = bestMirror;
        }
        else
        {
            LogWrapper.Debug("Downloader", $"使用默认镜像: {ActiveUri}");
        }

        LogWrapper.Debug("Downloader", $"正在获取文件大小: {ActiveUri}");
        using var response = await client
            .SendAsync(new HttpRequestMessage(HttpMethod.Head, ActiveUri), token)
            .ConfigureAwait(false);

        TotalSize = response.Content.Headers.ContentLength ?? 0;
        LogWrapper.Debug("Downloader", $"获取到文件大小: {TotalSize} 字节");

        lock (Segments)
        {
            Segments.Clear();
            var segment = new DownloadSegment(ActiveUri, TargetPath, 0, TotalSize > 0 ? TotalSize - 1 : null);
            Segments.Add(segment);
            LogWrapper.Debug("Downloader", $"创建初始分段: {segment.Start}-{segment.End}");
        }

        LogWrapper.Debug("Downloader", "下载任务准备完成");
    }

    /// <summary>
    /// 尝试分割一个下载分段
    /// </summary>
    /// <param name="minSpilitSize">最小分割大小</param>
    /// <returns>新分割的下载分段，若无法分割则返回 null</returns>
    public DownloadSegment? TrySplitSegment(long minSpilitSize = 1024 * 1024 * 2)
    {
        if (!SupportsRange)
        {
            LogWrapper.Trace("Downloader", "不支持范围请求，无法分割分段");
            return null;
        }

        lock (Segments)
        {
            var target = Segments
                .Where(s => s.Status is DownloadSegmentStatus.WaitingStart or DownloadSegmentStatus.Running)
                .OrderByDescending(s => s.RemainingBytes)
                .FirstOrDefault();

            if (target is null)
            {
                LogWrapper.Trace("Downloader", "没有可分割的分段");
                return null;
            }

            var currentPos = target.Start + target.Downloaded;
            var mid = currentPos + (target.RemainingBytes / 2);

            var newSeg = new DownloadSegment(ActiveUri, TargetPath, mid + 1, target.End);
            target.End = mid;
            Segments.Add(newSeg);
            LogWrapper.Debug("Downloader", $"分割分段: 原分段 {target.Start}-{target.End}, 新分段 {newSeg.Start}-{newSeg.End}");

            return newSeg;
        }
    }
}
