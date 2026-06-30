using System.Collections.Concurrent;
using System.IO;
using PCL.Core.App;
using PCL.Core.Utils;

namespace PCL.Network.Loaders;

public class LoaderDownload : ModLoader.LoaderBase
{
    public SafeList<DownloadFile> files;
    private int _fileRemain;
    private readonly object _fileRemainLock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    public int FailCount { get; set; }

    public override double Progress
    {
        get => State >= LoadState.Finished ? 1 : files.Any() ? files.Average(file => file.Progress) : 0;
        set => throw new Exception("文件下载不允许指定进度");
    }

    public LoaderDownload(string name, List<DownloadFile> fileTasks)
    {
        base.name = name;
        files = new SafeList<DownloadFile>(fileTasks ?? new List<DownloadFile>());
    }

    public void RefreshStat() { }

    public override void Start(object input = null, bool isForceRestart = false)
    {
        if (input is List<DownloadFile> inputFiles)
            files = new SafeList<DownloadFile>(inputFiles);

        lock (lockState)
        {
            if (State == LoadState.Loading)
                return;
            State = LoadState.Loading;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        lock (_fileRemainLock)
        {
            _fileRemain = files.Count;
        }

        ModNet.NetManager.Start(this);

        Basics.RunInNewThread(() => Run(_cancellationTokenSource.Token), $"DL/{Uuid}");
    }

    private void Run(CancellationToken cancellationToken)
    {
        try
        {
            if (!files.Any())
            {
                OnFinish();
                return;
            }

            var exceptions = new ConcurrentQueue<Exception>();
            using var semaphore = new SemaphoreSlim(GetMaxParallelFiles());
            var tasks = files.Select(async file =>
            {
                var entered = false;
                try
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    entered = true;
                    await ProcessFileAsync(file, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    file.Errors.Add(ex);
                    file.State = NetState.Interrupted;
                    exceptions.Enqueue(ex);
                    _cancellationTokenSource?.Cancel();
                }
                finally
                {
                    if (entered)
                        semaphore.Release();
                }
            }).ToList();

            Task.WhenAll(tasks).GetAwaiter().GetResult();
            if (!exceptions.IsEmpty)
                OnFail(exceptions.ToList());
        }
        catch (OperationCanceledException)
        {
            Abort();
        }
        catch (Exception ex)
        {
            OnFail(new List<Exception> { ex });
        }
    }

    private int GetMaxParallelFiles()
    {
        return Math.Max(1, Math.Min(files.Count, Math.Clamp(ModNet.NetTaskThreadLimit, 1, 64)));
    }

    private async Task ProcessFileAsync(DownloadFile file, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!file.Loaders.Contains(this))
            file.Loaders.Add(this);

        if (State >= LoadState.Finished)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(file.LocalPath) ?? throw new IOException("下载路径无效"));
        if (file.Check?.canUseExistsFile == true && file.Check.Check(file.LocalPath) is null)
        {
            file.IsCopy = true;
            file.State = NetState.Finished;
            try { file.TotalSize = new FileInfo(file.LocalPath).Length; }
            catch (IOException) { file.TotalSize = -1; }
            file.DownloadedBytes = file.TotalSize;
            file.Speed = 0;
            file.ActiveThreads = 0;
            OnFileFinish(file);
            return;
        }

        file.State = NetState.Connecting;
        var enableParallelChunks = files.Count <= 1;
        for (var retry = 0; retry < 4; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await FileDownloader.DownloadAsync(file.Urls, file.LocalPath, file.UseBrowserUserAgent, file.CustomUserAgent,
                    cancellationToken, enableParallelChunks, file).ConfigureAwait(false);
                break;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (retry < 3)
            {
                LauncherLog.Log(ex, $"[Download] 重试 {retry + 1}/3：{file.LocalPath}");
                Thread.Sleep(RandomUtils.NextInt(300, 500 + retry * 300));
            }
        }
        try { file.TotalSize = new FileInfo(file.LocalPath).Length; }
        catch (IOException) { file.TotalSize = -1; }
        file.IsUnknownSize = file.TotalSize < 0;
        file.DownloadedBytes = Math.Max(0, file.TotalSize);
        file.Speed = 0;
        file.ActiveThreads = 0;
        file.State = NetState.Finished;
        OnFileFinish(file);
    }

    public void OnFileFinish(DownloadFile file)
    {
        lock (_fileRemainLock)
        {
            _fileRemain -= 1;
            if (_fileRemain > 0)
                return;
        }

        OnFinish();
    }

    public void OnFinish()
    {
        RaisePreviewFinish();
        lock (lockState)
        {
            if (State > LoadState.Loading)
                return;
            State = LoadState.Finished;
        }

        ModNet.NetManager.Finish(this);
    }

    public void OnFileFail(DownloadFile file)
    {
        OnFail(file.Errors.Any() ? file.Errors : new List<Exception> { new Exception($"文件下载失败：{file.LocalPath}") });
    }

    public void OnFail(List<Exception> exList)
    {
        lock (lockState)
        {
            if (State > LoadState.Loading)
                return;
            Error = exList.FirstOrDefault() ?? new Exception("未知下载错误");
            State = LoadState.Failed;
        }

        FailCount += exList.Count;
        foreach (var file in files.Where(file => file.State < NetState.Finished))
        {
            file.State = NetState.Interrupted;
            file.Speed = 0;
            file.ActiveThreads = 0;
            file.Errors.AddRange(exList);
        }

        ModNet.NetManager.Finish(this);
    }

    public override void Abort()
    {
        lock (lockState)
        {
            if (State >= LoadState.Finished)
                return;
            State = LoadState.Aborted;
        }

        _cancellationTokenSource?.Cancel();
        foreach (var file in files.Where(file => file.State < NetState.Finished))
        {
            file.State = NetState.Interrupted;
            file.Speed = 0;
            file.ActiveThreads = 0;
        }

        ModNet.NetManager.Finish(this);
    }
}
