namespace PCL.Network;

public enum NetState
{
    WaitingToCheck = -1,
    WaitingToDownload = 0,
    Connecting = 1,
    Reading = 2,
    Downloading = 3,
    Merging = 4,
    Finished = 5,
    Interrupted = 6
}

public enum NetPreDownloadBehaviour
{
    HintWhileExists,
    ExitWhileExistsOrDownloading,
    IgnoreCheck
}
