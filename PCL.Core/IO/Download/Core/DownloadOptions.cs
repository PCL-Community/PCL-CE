using System;
using System.Collections.Generic;

namespace PCL.Core.IO.Download.Core;

/// <summary>
/// 下载任务配置
/// </summary>
/// <param name="MirrorUrls">下载任务的目标URL（包括镜像）</param>
/// <param name="DestinationFilePath">下载文件保存位置</param>
/// <param name="MaxConcurrentWorkers">最大并发数</param>
/// <param name="ChunkSizeBytes">分块大小</param>
/// <param name="MinSpeedThresholdBps">最小速率限制（用于自动切换镜像）</param>
/// <param name="SpeedCheckInterval">速率检查间隔</param>
/// <param name="MemoryBufferSizeBytes">缓冲块大小</param>
public record DownloadOptions(
    List<string> MirrorUrls,
    string DestinationFilePath,
    int MaxConcurrentWorkers = 4,
    long ChunkSizeBytes = 1024 * 1024 * 1, // 1 MB
    double MinSpeedThresholdBps = 1 * 1024, // 1 KB/s
    TimeSpan SpeedCheckInterval = default,
    int MemoryBufferSizeBytes = 1024 * 1024 * 10 // 10 MB
)
{
    /// <summary>
    /// 速率检查间隔
    /// </summary>
    public TimeSpan SpeedCheckInterval { get; init; } =
        SpeedCheckInterval == TimeSpan.Zero ? TimeSpan.FromSeconds(5) : SpeedCheckInterval;
}