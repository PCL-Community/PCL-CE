using System;
using System.Threading.Tasks;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.UI.Threading;

/// <summary>UI 线程访问与调度。具体实现由组合根注册到 <see cref="ApplicationRuntime" />。 运行时未初始化时保持安全降级行为。</summary>
public static class UiDispatcher
{
    private static IUiDispatcher? Dispatcher =>
        ApplicationRuntime.IsInitialized ? ApplicationRuntime.Current.Ui.Dispatcher : null;

    /// <summary>界面是否仍可交互（应用已创建且未开始关闭）。</summary>
    public static bool IsAvailable => Dispatcher is not null && !Lifecycle.HasShutdownStarted;

    public static bool CheckAccess()
    {
        return Dispatcher?.CheckAccess() ?? false;
    }

    public static void Post(Action action)
    {
        Dispatcher?.Post(action);
    }

    /// <summary>在 UI 线程同步执行指定操作，UI 不可用时直接返回。</summary>
    public static void Invoke(Action action)
    {
        Dispatcher?.Invoke(action);
    }

    /// <summary>在 UI 线程同步执行指定操作并返回结果，UI 不可用时返回默认值。</summary>
    public static T? Invoke<T>(Func<T> func)
    {
        return Dispatcher is null ? default : Dispatcher.Invoke(func);
    }

    public static Task InvokeAsync(Action action)
    {
        return Dispatcher?.InvokeAsync(action) ?? Task.CompletedTask;
    }

    public static Task<T> InvokeAsync<T>(Func<T> func)
    {
        return Dispatcher?.InvokeAsync(func) ?? Task.FromResult(default(T)!);
    }
}