using System;
using System.Threading.Tasks;

namespace PCL.Core.Platform;

/// <summary>
///     UI 线程访问与调度。具体实现由 UI 层注册到 <see cref="PlatformServices.UiDispatcher" />。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>当前线程是否为 UI 线程。</summary>
    bool CheckAccess();

    /// <summary>将操作排队到 UI 线程，不等待执行。</summary>
    void Post(Action action);

    /// <summary>在 UI 线程同步执行操作；UI 不可用时直接返回。</summary>
    void Invoke(Action action);

    /// <summary>在 UI 线程同步执行操作并返回结果；UI 不可用时返回默认值。</summary>
    T? Invoke<T>(Func<T> func);

    /// <summary>在 UI 线程异步执行操作；UI 不可用时返回已完成的任务。</summary>
    Task InvokeAsync(Action action);

    /// <summary>在 UI 线程异步执行操作并返回结果；UI 不可用时返回默认值。</summary>
    Task<T> InvokeAsync<T>(Func<T> func);
}