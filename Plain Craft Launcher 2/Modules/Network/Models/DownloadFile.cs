using PCL.Core.Utils;
using PCL.Network.Loaders;

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
    // Errors / Loaders 可能被多个并发下载加载器共享访问（同一 DownloadFile 可登记到多个加载器），
    // 故所有访问都经 _sync 加锁，避免普通 List 被并发读写而抛 InvalidOperationException。
    private readonly object _sync = new();
    private readonly List<Exception> _errors = new();
    private readonly List<LoaderDownload> _loaders = new();

    /// <summary>该文件下载过程中记录的错误（返回快照，线程安全）。</summary>
    public IReadOnlyList<Exception> Errors
    {
        get { lock (_sync) return _errors.ToArray(); }
    }

    /// <summary>已登记的下载加载器（返回快照，线程安全）。</summary>
    public IReadOnlyList<LoaderDownload> Loaders
    {
        get { lock (_sync) return _loaders.ToArray(); }
    }

    public void AddError(Exception error)
    {
        lock (_sync) _errors.Add(error);
    }

    public void AddErrors(IEnumerable<Exception> errors)
    {
        lock (_sync) _errors.AddRange(errors);
    }

    /// <summary>登记一个下载加载器；同一加载器只登记一次。返回是否为首次登记。</summary>
    public bool RegisterLoader(LoaderDownload loader)
    {
        lock (_sync)
        {
            if (_loaders.Contains(loader)) return false;
            _loaders.Add(loader);
            return true;
        }
    }
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
