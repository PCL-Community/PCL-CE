using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Threading;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.IO;

public static class PredefinedFileItems
{
    public static readonly FileItem CacheInformation = FileItem.FromLocalFile("cache.txt", FileType.Temporary);
    public static readonly FileItem GrayProfile = FileItem.FromLocalFile("gray.json", FileType.Data);
    public static readonly FileItem LocalSetup = FileItem.FromLocalFile("setup.ini", FileType.Data);
}

public static class PredefinedFileTasks
{
    public static readonly IFileTask CacheInformation = FileTask.FromSingleFile(PredefinedFileItems.CacheInformation, FileTransfers.DoNothing);
    public static readonly IFileTask GrayProfile = FileTask.FromSingleFile(PredefinedFileItems.GrayProfile, FileTransfers.DoNothing, FileProcesses.ParseJson<GrayProfileConfig>());
    
    internal static readonly IFileTask[] Preload = [
        CacheInformation, GrayProfile
    ];
}

public class ResultFailedException(Exception innerException) : Exception(innerException.Message, innerException);

/// <summary>
/// Global file management service.<br/>
/// <b>NOTE</b>: The behaviors of all path strings in this service depends on <see cref="Path"/> API
/// provided by .NET standard library. You should use <see cref="Path"/> and other APIs relative to it to
/// process any path string from this service, rather than concat paths manually.
/// </summary>
[LifecycleService(LifecycleState.Loading, Priority = 1919820)]
[LifecycleScope("file", "文件管理")]
public partial class FileService
{

    #region Paths

    /// <summary>
    /// The default directory used for relative path combining.
    /// </summary>
    public static string DefaultDirectory => Basics.ExecutableDirectory;

    private static string _dataPath;
    private static string _sharedDataPath;
    private static string _localDataPath;
    private static string _tempPath;
    
    /// <summary>
    /// Per-instance data directory.
    /// </summary>
    public static string DataPath { get => _dataPath; set => _dataPath = value; }

    /// <summary>
    /// Shared synchronized data directory.
    /// </summary>
    public static string SharedDataPath { get => _sharedDataPath; set => _sharedDataPath = value; }
    
    /// <summary>
    /// Shared synchronized data directory of old versions.<br/>
    /// Keep the value just for migration, DO NOT USE IT.
    /// </summary>
    public static string OldSharedDataPath { get; set; }

    /// <summary>
    /// Shared local data directory, used to put some large files that can be released or downloaded back anytime.
    /// </summary>
    public static string LocalDataPath { get => _localDataPath; set => _localDataPath = value; }
    
    /// <summary>
    /// Temporary files directory (can be deleted anytime, except when the program is running).
    /// </summary>
    public static string TempPath { get => _tempPath; set => _tempPath = value; }

    /// <summary>
    /// Get path string relative to a special folder.
    /// </summary>
    /// <param name="folder">the special folder</param>
    /// <param name="relative">the relative path</param>
    /// <returns>the path string relative to the special folder</returns>
    public static string GetSpecialPath(Special folder, string relative)
    {
        var folderPath = Environment.GetFolderPath(folder);
        return Path.Combine(folderPath, relative);
    }

    static FileService()
    {
#if DEBUG
        const string name = "PCLCE_Debug";
        const string oldName = ".PCLCEDebug";
#else
        const string name = "PCLCE";
        const string oldName = ".PCLCE";
#endif
        // fill paths
        _dataPath = Path.Combine(DefaultDirectory, "PCL");
        _sharedDataPath = GetSpecialPath(Special.ApplicationData, name);
        _localDataPath = GetSpecialPath(Special.LocalApplicationData, name);
        _tempPath = Path.Combine(Path.GetTempPath(), name);
        OldSharedDataPath = GetSpecialPath(Special.ApplicationData, oldName);
#if DEBUG
        // read environment variables
        EnvironmentInterop.ReadVariable("PCL_PATH", ref _dataPath);
        EnvironmentInterop.ReadVariable("PCL_PATH_SHARED", ref _sharedDataPath);
        EnvironmentInterop.ReadVariable("PCL_PATH_LOCAL", ref _localDataPath);
        EnvironmentInterop.ReadVariable("PCL_PATH_TEMP", ref _tempPath);
#endif
        // create directories
        Directory.CreateDirectory(_dataPath);
        Directory.CreateDirectory(_sharedDataPath);
        Directory.CreateDirectory(_localDataPath);
        Directory.CreateDirectory(_tempPath);
    }

    #endregion

    #region Lifecycle

    private static readonly CancellationTokenSource _CancellationToken = new();

    private static Thread? _fileLoadingThread;

    [LifecycleStart]
    public void Start()
    {
        // start load thread
        Context.Debug("正在启动文件加载守护线程");
        _fileLoadingThread = Basics.RunInNewThread(_FileLoadCallback, "Daemon/FileLoading");
        // invoke initialize
        _Initialize();
    }

    [LifecycleStop]
    public void Stop()
    {
        Context.Debug("正在停止文件处理工作");
        _CancellationToken.Cancel();
        _running = false;
        _fileLoadingThread?.Join();
    }

    #endregion

    #region Process

    private static readonly List<FileMatchPair<FileTransfer>> _DefaultTransfers = [];
    
    private static readonly List<FileMatchPair<FileProcess>> _DefaultProcesses = [];

    /// <summary>
    /// Register a transfer implementation with a match.
    /// </summary>
    public static void RegisterDefaultTransfer(FileMatch match, FileTransfer transfer)
        => _DefaultTransfers.Add(match.Pair(transfer));
    
    /// <summary>
    /// Register a process implementation with a match.
    /// </summary>
    public static void RegisterDefaultProcess(FileMatch match, FileProcess process)
        => _DefaultProcesses.Add(match.Pair(process));

    private static readonly ConcurrentQueue<IFileTask> _PendingTasks = [];
    
    private static readonly ConcurrentDictionary<FileItem, AtomicVariable<AnyType>> _ProcessResults = [];
    private static readonly ConcurrentDictionary<FileItem, AsyncManualResetEvent> _WaitForResultEvents = [];
    private static readonly AutoResetEvent _ContinueEvent = new(false);
    private static bool _running = true;

    private static void _FileLoadCallback()
    {
        int? threadLimit = null;
        EnvironmentInterop.ReadVariable("PCL_FILE_THREAD_LIMIT", ref threadLimit);
        
        // CPU 密集工作线程应使用性能内核的数量限制（超线程一并计入），防止跑到能效内核上
        // 如果这个死人调度还给往能效内核上扔就没法了，砍掉 Windows 即可解决
        threadLimit ??= KernelInterop.GetPerformanceLogicalProcessorCount();
        
        Context.Info($"以最多 {threadLimit} 个线程初始化线程池");
        var threadPool = new DualThreadPool((int)threadLimit);
        
        while (_running)
        {
            if (!_PendingTasks.TryDequeue(out var task))
            {
                var waited = WaitHandle.WaitAny([_ContinueEvent, _CancellationToken.Token.WaitHandle]);
                continue;
            }

            var items = task.Items.ToList();
            var count = items.Count;
            foreach (var item in items)
            {
                Context.Trace($"正在加载文件: {item}");
                var finishedCount = 0;
                var process = task.GetProcess(item) ?? _DefaultProcesses.MatchFirst(item);
                var targetPath = item.TargetPath;
                if (!item.ForceTransfer && File.Exists(targetPath)) PushProcess(targetPath);
                else
                {
                    var transfers = task.GetTransfer(item).Concat(_DefaultTransfers.MatchAll(item));
                    threadPool.QueueIo(() =>
                    {
                        if (
                            transfers.Any(transfer =>
                            {
                                try
                                {
                                    transfer(item, PushProcess);
                                    return true;
                                }
                                catch (TransferFailedException ex)
                                {
                                    Context.Info($"文件传输失败 ({ex.Reason}), 尝试另一实现", ex.InnerException);
                                    return false;
                                }
                                catch (Exception ex)
                                {
                                    Context.Warn($"文件传输出错: {item}", ex);
                                    OnProcessFinished(item, new AnyType(ex, true));
                                    return true;
                                }
                            })
                        ) return;
                        Context.Warn($"无支持的传输实现或全部失败: {item}");
                        OnProcessFinished(item, null);
                    });
                }
                continue;

                void PushProcess(string? path)
                {
                    threadPool.QueueCpu(() =>
                    {
                        object? result;
                        var isException = false;
                        if (process == null) result = null;
                        else
                        {
                            try { result = process(item, path); }
                            catch (Exception ex)
                            {
                                Context.Warn($"文件处理出错: {item}", ex);
                                result = ex;
                                isException = true;
                            }
                        }
                        OnProcessFinished(item, AnyType.FromNullable(result, isException));
                    });
                }

                void OnProcessFinished(FileItem finishedItem, AnyType? result, bool removeHandled = true)
                {
                    threadPool.QueueCpu(() =>
                    {
                        var atomicResult = new AtomicVariable<AnyType>(result, true, true);
                        _ProcessResults.AddOrUpdate(item, atomicResult, (_, _) => atomicResult);
                        // 触发等待事件
                        if (_WaitForResultEvents.TryRemove(finishedItem, out var waitEvent)) waitEvent.Set();
                        try
                        {
                            var handled = task.OnProcessFinished(finishedItem, result);
                            if (removeHandled && handled) _ProcessResults.TryRemove(finishedItem, out _);
                        }
                        catch (Exception ex)
                        {
                            Context.Error($"文件处理完成出错: {finishedItem}", ex);
                        }
                        if (++finishedCount != count) return;
                        try
                        {
                            task.OnTaskFinished(null);
                        }
                        catch (Exception ex)
                        {
                            Context.Error($"任务完成出错", ex);
                        }
                    });
                }
            }
        }
        
        Context.Debug("尝试取消所有正在运行的工作");
        threadPool.CancelAll();
    }

    /// <summary>
    /// Add tasks to the loading queue.
    /// </summary>
    /// <param name="tasks">the tasks to add</param>
    public static void QueueTask(params IFileTask[] tasks)
    {
        foreach (var task in tasks) _PendingTasks.Enqueue(task);
        _ContinueEvent.Set();
    }

    /// <param name="item">which file to get the result</param>
    /// <param name="result">a <b>nullable</b> value, also <c>null</c> if not succeed</param>
    /// <param name="remove">whether remove from the temp dictionary after successfully get the value</param>
    /// <param name="throwException">whether throws if the result has dropped into an exception</param>
    /// <returns><c>true</c> if succeeded, or <c>false</c> if no result</returns>
    public static bool TryGetResult(FileItem item, out AnyType? result, bool remove = true, bool throwException = false)
    {
        _ProcessResults.TryGetValue(item, out var atomicResult);
        if (atomicResult == null)
        {
            result = null;
            return false;
        }
        result = atomicResult.Value;
        if (remove) _ProcessResults.TryRemove(item, out _);
        if (throwException && result?.HasException == true) throw new ResultFailedException(result.LastException!);
        return true;
    }

    /// <param name="item">which file to get the result</param>
    /// <param name="remove">whether remove from the temp dictionary after successfully get the value</param>
    /// <returns>a <b>nullable</b> value</returns>
    /// <exception cref="KeyNotFoundException">no result</exception>
    public static AnyType? GetResult(FileItem item, bool remove = true)
    {
        if (TryGetResult(item, out var result, remove)) return result;
        throw new KeyNotFoundException($"No result yet: {item}");
    }

    /// <param name="item">which file to wait for the result</param>
    /// <param name="timeout">the maximum waiting time</param>
    /// <param name="remove">whether remove from the temp dictionary after successfully get the value</param>
    /// <param name="throwException">whether throws if the result has dropped into an exception</param>
    /// <returns>
    /// a value, or <c>null</c> if the result is really <c>null</c>, or else, something is boom -
    /// I don't know what is wrong but in a word there is something wrong :D
    /// </returns>
    public static AnyType? WaitForResult(FileItem item, TimeSpan? timeout = null, bool remove = true, bool throwException = false)
    {
        var success = TryGetResult(item, out var result, remove, throwException);
        if (success) return result;
        var waitEvent = _WaitForResultEvents.GetOrAdd(item, _ => new AsyncManualResetEvent());
        var waitResult = true;
        if (timeout is { } t) waitResult = waitEvent.Wait(t);
        else waitEvent.Wait();
        if (!waitResult) return null;
        TryGetResult(item, out result, remove, throwException);
        return result;
    }

    /// <param name="item">which file to wait for the result</param>
    /// <param name="cancelToken">the cancellation token to stop waiting</param>
    /// <param name="remove">whether remove from the temp dictionary after successfully get the value</param>
    /// <param name="throwException">whether throws if the result has dropped into an exception</param>
    /// <returns>
    /// a value, or <c>null</c> if the result is really <c>null</c>, or else, something is boom -
    /// I don't know what is wrong but in a word there is something wrong :D
    /// </returns>
    public static async Task<AnyType?> WaitForResultAsync(FileItem item, CancellationToken cancelToken = default, bool remove = true, bool throwException = false)
    {
        var success = TryGetResult(item, out var result, remove, throwException);
        if (success) return result;
        var waitEvent = _WaitForResultEvents.GetOrAdd(item, _ => new AsyncManualResetEvent());
        var waitResult = true;
        cancelToken.Register(() => waitResult = false);
        await waitEvent.WaitAsync(cancelToken);
        if (!waitResult) return null;
        TryGetResult(item, out result, remove, throwException);
        return result;
    }

    #endregion

    private static void _Initialize()
    {
        // processes
        RegisterDefaultProcess(FileMatches.WithNameExtension("txt"), FileProcesses.ReadText);
        
        // transfers
        // TODO
        
        // preload tasks
        QueueTask(PredefinedFileTasks.Preload);
    }

}
