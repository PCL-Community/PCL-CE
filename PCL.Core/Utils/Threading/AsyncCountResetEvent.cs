using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PCL.Core.Utils.Threading;

// 使用 AI 生成的代码
// 时间: 2025/9/2
// 模型: GPT-5

/// <summary>
/// 一个带配额（Permit）的 <see cref="System.Threading.AutoResetEvent"/> 变体。
/// 支持一次性释放多个等待任务。
/// 类似于 <see cref="System.Threading.SemaphoreSlim"/>，但语义更接近 AutoResetEvent。
/// </summary>
public sealed class AsyncCountResetEvent : IDisposable
{
    private readonly Queue<TaskCompletionSource<bool>> _waiters = new();
    private readonly object _lock = new();

    /// <summary>
    /// 当前剩余的配额数。如果 &gt; 0，新的等待者会立即通过。
    /// </summary>
    private int _permits;

    /// <summary>
    /// 标记当前对象是否已释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 析构函数。
    /// </summary>
    ~AsyncCountResetEvent()
    {
        Dispose();
    }
    
    /// <summary>
    /// 等待一个信号。当信号可用时返回完成的 <see cref="Task"/>。
    /// 如果没有信号，则进入队列等待。
    /// </summary>
    /// <returns>
    /// 一个 <see cref="Task"/>，表示等待操作。
    /// 如果对象被释放，则返回的 Task 会异常结束。
    /// </returns>
    public Task WaitAsync()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(AsyncCountResetEvent));

            if (_permits > 0)
            {
                _permits--;
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Enqueue(tcs);
            return tcs.Task;
        }
    }

    /// <summary>
    /// 释放一个或多个信号，让等待者继续执行。
    /// </summary>
    /// <param name="count">要释放的配额数量，默认为 1。</param>
    public void Set(int count = 1)
    {
        if (count <= 0) return;

        lock (_lock)
        {
            if (_disposed) return;

            while (count > 0 && _waiters.Count > 0)
            {
                var tcs = _waiters.Dequeue();
                tcs.TrySetResult(true);
                count--;
            }

            // 如果没有等待者，就累积到配额里
            _permits += count;
        }
    }

    /// <summary>
    /// 释放当前对象。
    /// 会让所有等待中的任务异常完成。
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            while (_waiters.Count > 0)
            {
                var tcs = _waiters.Dequeue();
                tcs.TrySetException(new ObjectDisposedException(nameof(AsyncCountResetEvent)));
            }
        }

        GC.SuppressFinalize(this);
    }
}
