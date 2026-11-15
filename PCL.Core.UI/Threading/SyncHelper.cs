using Avalonia.Threading;

namespace PCL.Core.UI;

/// <summary>
/// 把 Avalonia 的异步 API 适配成 <see cref="PCL.Core.Platform" /> 中的同步契约。 UI
/// 线程上会边等待边执行已排队的调度任务，避免同步等待造成的死锁。
/// </summary>
internal static class SyncHelper
{
    /// <summary>同步等待任务完成并返回结果；UI 线程上会持续驱动调度队列。</summary>
    public static T? Wait<T>(Task<T> task)
    {
        WaitCore(task);
        return task.GetAwaiter().GetResult();
    }

    /// <summary>同步等待任务完成；UI 线程上会持续驱动调度队列。</summary>
    public static void Wait(Task task)
    {
        WaitCore(task);
        task.GetAwaiter().GetResult();
    }

    private static void WaitCore(Task task)
    {
        if (task.IsCompleted) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            task.GetAwaiter().GetResult();
            return;
        }

        var frame = new DispatcherFrame(false);
        task.ContinueWith(
            static (_, state) => ((DispatcherFrame)state!).Continue = false,
            frame,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        if (!task.IsCompleted)
            Dispatcher.UIThread.PushFrame(frame);
    }
}