using System;
using System.Threading.Tasks;

namespace PCL.Core.UI.Animation.UIAccessProvider;

/// <summary>
/// 跨框架的 UI 线程访问接口。
/// </summary>
public interface IUIAccessProvider
{
    /// <summary>
    /// 是否在 UI 线程。
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// 在 UI 线程同步执行。
    /// 如果当前就在 UI 线程，则直接执行。
    /// </summary>
    void Invoke(Action action);

    /// <summary>
    /// 在 UI 线程异步执行。
    /// 如果当前就在 UI 线程，则直接执行。
    /// </summary>
    Task InvokeAsync(Action action);

    /// <summary>
    /// 在 UI 线程异步执行，并返回结果。
    /// </summary>
    Task<T> InvokeAsync<T>(Func<T> func);

    /// <summary>
    /// 在 UI 线程异步执行 Task。
    /// </summary>
    Task InvokeAsync(Func<Task> func);

    /// <summary>
    /// 在 UI 线程异步执行 Task，并返回结果。
    /// </summary>
    Task<T> InvokeAsync<T>(Func<Task<T>> func);

    /// <summary>
    /// UI 渲染时执行的事件。
    /// </summary>
    event EventHandler FrameTick;
}