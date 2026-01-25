using System;
using PCL.Core.Logging;

namespace PCL.Core.App;

/// <summary>
/// 若要获取服务项自身的上下文实例，请使用 <see cref="Lifecycle.GetContext"/> 。
/// </summary>
public class LifecycleContext(
    ILifecycleService service,
    Action<LifecycleLogItem> onLog,
    Action<int> onRequestExit,
    Action<string?> onRequestRestart,
    Action onDeclareStopped,
    Action onRequestStopLoading)
{
    #region 日志输出
    
    public void CustomLog(
        string message,
        Exception? ex = null,
        LogLevel level = LogLevel.Trace,
        ActionLevel? actionLevel = null
    ) => onLog(new LifecycleLogItem(service, message, ex, level, actionLevel ?? level.DefaultActionLevel()));
    
    public void Trace(string message, Exception? ex = null, ActionLevel? actionLevel = null) => CustomLog(message, ex, LogLevel.Trace, actionLevel);
    public void Debug(string message, Exception? ex = null, ActionLevel? actionLevel = null) => CustomLog(message, ex, LogLevel.Debug, actionLevel);
    public void Info(string message, Exception? ex = null, ActionLevel? actionLevel = null) => CustomLog(message, ex, LogLevel.Info, actionLevel);
    public void Warn(string message, Exception? ex = null, ActionLevel? actionLevel = null) => CustomLog(message, ex, LogLevel.Warning, actionLevel);
    public void Error(string message, Exception? ex = null, ActionLevel? actionLevel = null) => CustomLog(message, ex, LogLevel.Error, actionLevel);
    public void Fatal(string message, Exception? ex = null, ActionLevel? actionLevel = null) => CustomLog(message, ex, LogLevel.Fatal, actionLevel);
    
    #endregion

    /// <summary>
    /// 服务项自身实例。
    /// </summary>
    public ILifecycleService ServiceInstance => service;

    /// <summary>
    /// 请求退出程序。仅可在 <see cref="LifecycleState.BeforeLoading"/> 状态调用。
    /// </summary>
    /// <param name="statusCode">程序返回的状态码</param>
    /// <exception cref="InvalidOperationException">尝试在非 <see cref="LifecycleState.BeforeLoading"/> 状态调用</exception>
    public void RequestExit(int statusCode = 0) => onRequestExit(statusCode);
    
    /// <summary>
    /// 请求在程序退出时重启。调用该方法后，程序将在正常退出流程中自动执行重启，通常与退出程序结合使用。
    /// </summary>
    /// <param name="arguments">重启进程时使用的命令行参数</param>
    public void RequestRestartOnExit(string? arguments = null) => onRequestRestart(arguments);
    
    /// <summary>
    /// 标记自身已经结束运行。调用该方法将会直接从正在运行列表中移除该服务项，后续的
    /// <c>Stop</c> 等均不会触发。仅可在服务启动阶段即 <c>Start</c> 方法结束前调用。
    /// </summary>
    /// <exception cref="InvalidOperationException">尝试在非启动阶段调用</exception>
    public void DeclareStopped() => onDeclareStopped();
    
    /// <summary>
    /// 请求停止继续加载。多用于初始阶段在非 STA 线程中运行的服务接管进程整个生命周期，仅可在
    /// <see cref="LifecycleState.BeforeLoading"/> 状态调用。
    /// </summary>
    /// <exception cref="InvalidOperationException">尝试在非 <see cref="LifecycleState.BeforeLoading"/> 状态调用</exception>
    public void RequestStopLoading() => onRequestStopLoading();
}
