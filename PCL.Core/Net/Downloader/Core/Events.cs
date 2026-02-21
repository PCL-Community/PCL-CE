using System;

namespace PCL.Core.Net.Downloader.Core;

public delegate void DownloadStateChangeEventHandler(object sender, DownloadStateChangeEventArgs e);

public delegate void MirrorSwitchedEventHandler(object sender, MirrorSwitchedEventArgs e);

public delegate void DownloadProgressEventHandler(object sender, DownloadProgressEventArgs e);

public class DownloadStateChangeEventArgs : EventArgs
{
    public required DownloadState OldState { get; init; }
    public required DownloadState NewState { get; init; }
}

public class MirrorSwitchedEventArgs : EventArgs
{
    public required string OldMirrorUrl { get; init; }
    public required string NewMirrorUrl { get; init; }
    public required string Reason { get; init; }
}

public class DownloadProgressEventArgs : EventArgs
{
    public long TotalBytes { get; init; }
    public long DownloadedBytes { get; init; }
    public double CurrentSpeedBytesPerSecond { get; init; }
    public double ProgressPercentage => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
}