namespace PCL.Core.Net.Downloader;

/// <summary>
/// 下载分段状态
/// </summary>
public enum DownloadSegmentStatus
{
    WaitingStart,
    Running,
    Success,
    Cancelled,
    Failed
}