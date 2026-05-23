using PCL.Core.App.Tasks;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;

namespace PCL.Network.Loaders;

public class DownloadTask : ITask, ITaskCancelable, ITaskProgressive
{
    public ConcurrentBag<DownloadFile> Files;
    private readonly object _fileRemainLock = new();
    private readonly object _stateLock = new();
    private int _fileRemain;
    private double _progress;
    private CancellationTokenSource _cancellationTokenSource = new();
    public int FailCount { get; set; }

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public TaskState State { get; set; }

    public DownloadTask(string title, IEnumerable<DownloadFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        Title = title;
        Files = new ConcurrentBag<DownloadFile>(files);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        lock (_stateLock)
        {
            if (State is TaskState.Waiting)
            {
                return Task.CompletedTask;
            }

            _UpdateState(TaskState.Waiting, string.Empty);
        }

        lock (_fileRemainLock)
        {
            _fileRemain = Files.Count;
        }

        ModNet.NetManager.Start(this);
        RefreshState();

        return _WorkflowAsync(_cancellationTokenSource.Token);
    }

    private async Task _WorkflowAsync(CancellationToken ct)
    {
        try
        {
            if (Files.Count == 0)
            {
                OnFinish();
                return;
            }

            var exceptions = new ConcurrentQueue<Exception>();
            using var semaphore = new SemaphoreSlim(_GetMaxParallelFiles());

            var tasks = Files.Select(async file =>
            {
                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await _ProcessFileAsync(file, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // ignore
                }
                catch (Exception ex)
                {
                    file.Errors.Add(ex);
                    file.State = NetState.Interrupted;
                    exceptions.Enqueue(ex);
                    await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                    RefreshState();
                }
            }).ToImmutableArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (!exceptions.IsEmpty)
            {
                OnFail(exceptions.ToList());
            }
        }
        catch (OperationCanceledException)
        {
            Cancel();
        }
        catch (Exception ex)
        {
            OnFail([ex]);
        }
    }

    public void RefreshState()
    {
        if (Files.Count == 0)
        {
            _progress = 0;
            return;
        }

        _progress = Files.Average(file => file.Progress);
    }

    private void _UpdateState(TaskState newState, string msg)
    {
        State = newState;
        StateChanged?.Invoke(newState, msg);
    }

    private int _GetMaxParallelFiles() =>
        Math.Max(1, Math.Min(Files.Count, Math.Clamp(ModNet.NetTaskThreadLimit, 1, 64)));

    private async Task _ProcessFileAsync(DownloadFile file, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!file.Loaders.Contains(this))
        {
            file.Loaders.Add(this);
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(file.LocalPath) ?? throw new IOException("Invalid destination."));

        if (file.Check?.CanUseExistsFile == true && file.Check.Check(file.LocalPath) is null)
        {
            file.IsCopy = true;
            file.State = NetState.Finished;
            file.TotalSize = new FileInfo(file.LocalPath).Length;
            file.DownloadedBytes = file.TotalSize;
            file.Speed = 0;
            file.ActiveThreads = 0;

            OnFileFinish(); // in the original code, this method have a 'DownloadFile' argument, but it is not used, so I removed it.

            return;
        }

        file.State = NetState.Connecting;
        var enableParallelChunks = Files.Count <= 1;

        await FileDownloader.Download(file.Urls,
            file.LocalPath,
            file.UseBrowserUserAgent,
            file.CustomUserAgent,
            ct,
            enableParallelChunks,
            file).ConfigureAwait(false);

        file.TotalSize = File.Exists(file.LocalPath) ? new FileInfo(file.LocalPath).Length : -1;
        file.IsUnknownSize = file.TotalSize < 0;
        file.DownloadedBytes = Math.Max(0, file.TotalSize);
        file.Speed = 0;
        file.ActiveThreads = 0;
        file.State = NetState.Finished;

        OnFileFinish(); // same as above
    }

    public void OnFileFinish()
    {
        lock (_fileRemainLock)
        {
            _fileRemain -= 1;
            if (_fileRemain > 0)
            {
                return;
            }
        }

        OnFinish();
    }

    public void OnFinish()
    {
        //RaisePreviewFinish(); should the caller handle 'StateChanged' event to do other things when state is 'Success'
        lock (_stateLock)
        {
            if (State > TaskState.Waiting)
            {
                return;
            }

            _UpdateState(TaskState.Success, string.Empty);
        }

        ModNet.NetManager.Finish(this);
    }

    public void OnFileFail(DownloadFile file)
    {
        OnFail(file.Errors.Count > 0 ? file.Errors : [new Exception($"File download failed. {file.LocalPath}")]);
    }

    public void OnFail(IEnumerable<Exception> exceptions)
    {
        lock (_stateLock)
        {
            if (State > TaskState.Waiting)
            {
                return;
            }

            _UpdateState(TaskState.Failed, "Unknown error");
        }
    }

    /// <inheritdoc />
    public event TaskStateEvent? StateChanged;

    [Obsolete("Use Cancel() instead.")]
    public void Abort() => Cancel();

    /// <inheritdoc />
    public void Cancel()
    {
        lock (_stateLock)
        {
            if (State >= TaskState.Success)
            {
                return;
            }

            _UpdateState(TaskState.Canceled, string.Empty);
        }

        _cancellationTokenSource.Cancel();

        foreach (var file in Files.Where(file => file.State < NetState.Finished))
        {
            file.State = NetState.Interrupted;
            file.Speed = 0;
            file.ActiveThreads = 0;
        }

        ModNet.NetManager.Finish(this);
    }

    /// <inheritdoc />
    public event TaskProgressEvent? ProgressChanged;
}
