using System;
using System.Collections.Generic;

namespace PCL.Core.IO.Download.Core;

public record DownloadOptions(
    List<string> MirrorUrls,
    string DestinationFilePath,
    int MaxConcurrentWorkers = 4,
    long ChunkSizeBytes = 1024 * 1024 * 1, // 1 MB
    double MinSpeedThresholdBps = 1 * 1024, // 1 KB/s
    TimeSpan SpeedCheckInterval = default,
    TimeSpan TimeOut = default,
    int MemoryBufferSizeBytes = 1024 * 1024 * 10 // 10 MB
)
{
    public TimeSpan SpeedCheckInterval { get; init; } =
        SpeedCheckInterval == TimeSpan.Zero ? TimeSpan.FromSeconds(5) : SpeedCheckInterval;

    public TimeSpan TimeOut { get; init; } =
        TimeOut == TimeSpan.Zero ? TimeSpan.FromMinutes(10) : TimeOut;
}