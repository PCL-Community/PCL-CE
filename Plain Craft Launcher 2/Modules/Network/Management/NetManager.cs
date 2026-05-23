using PCL.Core.Utils;

namespace PCL.Network;

public sealed class NetManager
{
    private static readonly Lazy<NetManager> _instance = new(() => new NetManager());
    public static NetManager Instance => _instance.Value;

    public Dictionary<string, DownloadFile> Files { get; } = [];
    public ConcurrentSet<PCL.Network.Loaders.DownloadTask> Tasks { get; } = [];
    public object LockFiles { get; } = new();
    public object LockRemain { get; } = new();
    public int FileRemain
    {
        get
        {
            lock (LockFiles)
                return Files.Values.Count(file => file.State != NetState.Finished);
        }
    }
    private long _downloadDone;
    public object LockDone { get; } = new();
    public long DownloadDone
    {
        get
        {
            lock (LockDone)
                return _downloadDone;
        }
        set
        {
            lock (LockDone)
                _downloadDone = value;
        }
    }

    public long Speed
    {
        get
        {
            lock (LockFiles)
                return Files.Values.Sum(file => file.Speed);
        }
    }

    public int ThreadCount
    {
        get
        {
            lock (LockFiles)
                return Files.Values.Sum(file => file.ActiveThreads);
        }
    }

    public void Start(PCL.Network.Loaders.DownloadTask task)
    {
        lock (LockFiles)
        {
            Tasks.Remove(task);
            Tasks.Add(task);
            foreach (var file in task.Files)
                Files[file.LocalPath] = file;
        }
    }

    public void Finish(PCL.Network.Loaders.DownloadTask task)
    {
        lock (LockFiles)
        {
            Tasks.Remove(task);
            foreach (var file in task.Files)
                Files.Remove(file.LocalPath);
        }
    }
}
