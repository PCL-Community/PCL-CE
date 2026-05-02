using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace PCL;

/// <summary>
/// Owns dispatcher, UI-thread, background-thread, RunInUi, and RunInUiWait helpers.
/// </summary>
public static class LauncherDispatcher
{
    private static readonly object UuidLock = new();
    private static readonly TimeSpan UiInvokeTimeout = TimeSpan.FromSeconds(30);
    private static int _uuid = 1;
    private static readonly int UiThreadId = Thread.CurrentThread.ManagedThreadId;

    public static int GetUuid()
    {
        lock (UuidLock)
        {
            _uuid += 1;
            return _uuid;
        }
    }

    public static T RunInUiWait<T>(Func<T> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || RunInUi())
            return action();

        return dispatcher.Invoke(action, DispatcherPriority.Send, CancellationToken.None, UiInvokeTimeout);
    }

    public static void RunInUiWait(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || RunInUi())
            action();
        else
            dispatcher.Invoke(action, DispatcherPriority.Send, CancellationToken.None, UiInvokeTimeout);
    }

    public static void RunInUi(Action action, bool forceWaitUntilLoaded = false)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || RunInUi())
            action();
        else
            dispatcher.InvokeAsync(action,
                forceWaitUntilLoaded ? DispatcherPriority.Loaded : DispatcherPriority.Normal);
    }

    public static Thread RunInNewThread(Action action, string? name = null,
        ThreadPriority priority = ThreadPriority.Normal)
    {
        var threadName = name ?? GetThreadName("Runtime New Invoke");
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (ThreadInterruptedException)
            {
                LauncherLogger.Log(threadName + "：线程已中止");
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, threadName + "：线程执行失败", LauncherLogger.LogLevel.Feedback);
            }
        }) { Name = threadName, Priority = priority };
        thread.Start();
        return thread;
    }

    public static void RunInThread(Action action)
    {
        if (RunInUi())
            RunInNewThread(action, GetThreadName("Runtime Invoke"));
        else
            action();
    }

    public static bool IsUiThread()
    {
        return RunInUi();
    }

    public static bool IsUiThread(Thread thread)
    {
        return thread.ManagedThreadId == UiThreadId;
    }

    public static string GetThreadName(string prefix)
    {
        return prefix + " " + GetUuid() + "#";
    }

    public static bool RunInUi()
    {
        return Thread.CurrentThread.ManagedThreadId == UiThreadId;
    }
}
