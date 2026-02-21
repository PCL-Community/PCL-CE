namespace PCL.Core.Net.Downloader.Core;

public enum DownloadState
{
    Preparing,
    Probing,
    Downloading,
    Stalled,
    Completed,
    Failed
}