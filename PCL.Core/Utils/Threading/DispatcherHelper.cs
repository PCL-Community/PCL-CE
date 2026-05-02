using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PCL.Core.Utils.Threading;

public static class DispatcherHelper
{
    /// <exception cref="InvalidOperationException" accessor="get">Failed to get UI thread ID</exception>
    public static int? UiThreadId
    {
        get
        {
            if (field is not null)
            {
                return field;
            }

            if (GetUiThreadId is null)
            {
                throw new InvalidOperationException("Failed to get UI thread ID");

            }

            field = GetUiThreadId();
            return field;

        }
    }

    public static Func<int>? GetUiThreadId;

    public static bool IsRunInUi => Environment.CurrentManagedThreadId == UiThreadId;

    public static void InvokeInUiThread(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (Application.Current is null)
        {
            return;
        }

        if (IsRunInUi)
        {
            action();
        }
        else
        {
            Dispatcher.CurrentDispatcher.Invoke(action, priority);
        }
    }

    public static Task InvokeInUiThreadAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (Application.Current is null)
        {
            return Task.CompletedTask;
        }

        if (IsRunInUi)
        {
            action();
            return Task.CompletedTask;
        }
        else
        {
            return Dispatcher.CurrentDispatcher.InvokeAsync(action, priority).Task;
        }
    }


}