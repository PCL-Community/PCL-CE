using PCL.Core.Utils;

namespace PCL.Network;

public class DownloadFile
{
    public int Id { get; } = ModBase.GetUuid();
    public string LocalPath { get; set; }
    public string LocalName { get; }
    public List<string> Urls { get; }
    public ModBase.FileChecker? Check { get; }
    public bool UseBrowserUserAgent { get; }
    public string CustomUserAgent { get; }
    public NetState State { get; set; } = NetState.WaitingToCheck;
    public long TotalSize { get; set; } = -1;
    public bool IsUnknownSize { get; set; } = true;
    public long DownloadedBytes { get; set; }
    public bool IsCopy { get; set; }
    public List<Exception> Errors { get; } = new();
    public List<PCL.Network.Loaders.LoaderDownload> Loaders { get; } = new();
    public long Speed { get; set; }
    public int ActiveThreads { get; set; }
    public double Progress
    {
        get
        {
            return State switch
            {
                NetState.WaitingToCheck => 0,
                NetState.WaitingToDownload => 0.01,
                NetState.Connecting => 0.02,
                NetState.Reading => 0.04,
                NetState.Downloading when TotalSize > 0 => Math.Clamp((double)DownloadedBytes / TotalSize, 0.05, 1),
                NetState.Downloading => 0.5,
                NetState.Merging => 0.99,
                NetState.Finished or NetState.Interrupted => 1,
                _ => 0
            };
        }
    }

    public DownloadFile(IEnumerable<string> urls, string localPath, ModBase.FileChecker? checker = null,
        bool useBrowserUserAgent = false, string customUserAgent = "")
    {
        Urls = urls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct().ToList();
        LocalPath = localPath;
        LocalName = ModBase.GetFileNameFromPath(localPath);
        Check = checker;
        UseBrowserUserAgent = useBrowserUserAgent;
        CustomUserAgent = customUserAgent;
    }
}
