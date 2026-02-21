namespace PCL.Core.IO.Download.Core;

public enum DownloadState
{
    Preparing,
    Probing,
    Waiting,
    Downloading,
    Completed,
    Failed
}