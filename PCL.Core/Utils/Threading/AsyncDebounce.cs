using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Utils.Threading;

/// <summary>
/// 可重置的异步延时器。在指定延时后执行异步任务并等待下一次重置后重复该逻辑，指定延时未到达时重置将会重新开始计时。
/// <p>实例创建后并不会立即开始计时，而是等待第一次 <see cref="Reset"/>
/// 调用。因此，若有特殊需求，请不要忘了创建实例后调用一次 <see cref="Reset"/>。</p>
/// </summary>
public class AsyncDebounce(CancellationToken cancelToken = default) : IDisposable
{
    /// <summary>
    /// 执行延迟。
    /// </summary>
    public required TimeSpan Delay { get; init; }

    /// <summary>
    /// 异步任务实例。
    /// </summary>
    public required Func<Task> ScheduledTask { get; init; }

    /// <summary>
    /// 指示本次延迟任务是否已经完成。
    /// </summary>
    public bool IsCurrentTaskCompleted { get; private set; }

    /// <summary>
    /// 指示本次延迟任务是否正在运行。
    /// </summary>
    public bool IsCurrentTaskRunning => _currentTask != null;

    private Task? _currentTask;
    private Task? _worker; // 跟踪最近一次 worker
    private CancellationTokenSource? _currentDelayCts;
    private readonly CancellationTokenSource _cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
    private readonly object _resetLock = new();

    /// <summary>
    /// 重置延时。
    /// </summary>
    public async Task Reset()
    {
        IsCurrentTaskCompleted = false;

        CancellationTokenSource? capturedCts;
        Task? runningToAwait;

#pragma warning disable VSTHRD103 // 禁用检查 避免智障警告
        lock (_resetLock)
        {
            // 只取消，不在这里 Dispose
            try { _currentDelayCts?.Cancel(); }
            catch(ObjectDisposedException) { /* ignored */ }

            _currentDelayCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            capturedCts = _currentDelayCts;

            // 记录当前运行中的 ScheduledTask，稍后在锁外等待，避免重叠
            runningToAwait = _currentTask;

            _worker = Task.Run(async () =>
            {
                try { await Task.Delay(Delay, capturedCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                finally
                {
                    // 仅由使用者自己释放，避免跨线程 Dispose 竞态
                    // 注意：不要在这里释放 _cts
                    capturedCts.Dispose();
                }

                // 身份校验，确保自己仍是“当前”那一次
                if (
                    !ReferenceEquals(_currentDelayCts, capturedCts) ||
                    capturedCts.IsCancellationRequested ||
                    _cts.IsCancellationRequested
                ) return;

                if (_currentTask is not null) await _currentTask.ConfigureAwait(false);

                var task = ScheduledTask();
                lock (_resetLock) _currentTask = task;

                try
                {
                    await task.ConfigureAwait(false);
                }
                finally
                {
                    lock (_resetLock)
                    {
                        _currentTask = null;
                        IsCurrentTaskCompleted = true;
                    }
                }
            }, _cts.Token);
        }
#pragma warning restore VSTHRD103

        // 避免 ScheduledTask 并发
        if (runningToAwait is not null) await runningToAwait.ConfigureAwait(false);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _worker?.Wait(); } catch { /* ignored */ }
        _currentDelayCts?.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
