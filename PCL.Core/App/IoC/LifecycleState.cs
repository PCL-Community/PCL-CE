using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.IoC;

partial class Lifecycle
{
    /// <summary>
    /// 生命周期状态改变时触发的事件。<br/>
    /// <b>非异步执行，请注意自行实现必要的异步，否则会卡住生命周期管理线程。</b>
    /// </summary>
    public static event Action<LifecycleState>? StateChanged;

    /// <summary>
    /// 当前的生命周期状态，会随生命周期变化随时更新。
    /// </summary>
    public static LifecycleState CurrentState
    {
        get;
        private set
        {
            Context.Debug($"状态改变: {value}");
            field = value;
            try
            {
                StateChanged?.Invoke(value);
            }
            catch (Exception ex)
            {
                Context.Warn("状态更改事件出错", ex);
            }
        }
    } = LifecycleState.BeforeLoading;

    private static void _NextState(LifecycleState? enforce = null)
    {
        if (enforce is { } state) CurrentState = state;
        else CurrentState++;
    }

    /// <summary>
    /// 阻塞当前线程并等待到达指定生命周期状态。
    /// </summary>
    /// <param name="state">指定生命周期状态</param>
    /// <returns>
    /// 是否真正“等待”过（若调用该方法时已经到达或晚于指定状态，则为 <c>false</c>）
    /// </returns>
    public static bool WaitForState(LifecycleState state)
    {
        if (CurrentState >= state) return false; // 如果已经是目标状态，直接返回
        using var mre = new ManualResetEventSlim(false);
        StateChanged += TempHandler;
        try { mre.Wait(); } // 等待 Set() 方法
        finally { StateChanged -= TempHandler; } // 取消订阅，避免内存泄漏或重复唤醒
        return true;

        void TempHandler(LifecycleState s)
        {
            // ReSharper disable once AccessToDisposedClosure
            if (s == state) mre.Set();
        }
    }

    /// <summary>
    /// 异步等待到达指定生命周期状态。
    /// </summary>
    /// <param name="state">指定生命周期状态</param>
    /// <returns>
    /// 结果表示是否真正“等待”过的 <see cref="Task"/> 实例（若调用该方法时已经到达或晚于指定状态，则结果为 <c>false</c>）
    /// </returns>
    public static Task<bool> WaitForStateAsync(LifecycleState state)
    {
        if (CurrentState >= state) return Task.FromResult(false); // 如果已经是目标状态，则直接返回 false
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        StateChanged += TempHandler;
        return tcs.Task;

        void TempHandler(LifecycleState s)
        {
            if (s != state) return;
            StateChanged -= TempHandler;
            tcs.TrySetResult(true);
        }
    }

    /// <summary>
    /// 快速注册改变到目标生命周期状态的事件，与直接注册 <see cref="StateChanged"/> 的区别是会自动判断目标状态并自动移除事件注册。
    /// </summary>
    /// <param name="when">目标生命周期状态</param>
    /// <param name="action">事件触发委托</param>
    public static void When(LifecycleState when, Action action)
    {
        if (CurrentState >= when) return;
        StateChanged += TempHandler;
        return;

        void TempHandler(LifecycleState state)
        {
            if (state != when) return;
            action();
            StateChanged -= TempHandler;
        }
    }
}

/// <summary>
/// 生命周期状态
/// </summary>
public enum LifecycleState
{
    /// <summary>
    /// <b>手动运行</b><br/>
    /// 表示不应由生命周期管理自动运行。拥有该状态的服务项可以使用 <see cref="Lifecycle.StartService"/>
    /// 和 <see cref="Lifecycle.StopService"/> 手动控制启动和停止。<br/>
    /// 非异步启动可能在任意线程执行。
    /// </summary>
    Manual,
    
    /// <summary>
    /// <b>预加载</b><br/>
    /// 一些提前运行的无需使用基本组件的事件，如检测单例、提权进程、更新等。
    /// 在该状态运行的服务可以使用 <see cref="LifecycleContext.RequestExit"/> 请求直接退出程序。<br/>
    /// 非异步启动将在 STA 线程执行。
    /// </summary>
    BeforeLoading,
    
    /// <summary>
    /// <b>加载</b><br/>
    /// 基本组件初始化，如日志、系统基本信息、设置项等。<br/>
    /// 非异步启动将在 STA 线程执行。
    /// </summary>
    Loading,
    
    /// <summary>
    /// <b>加载结束</b><br/>
    /// 非基本组件初始化，大多数功能性组件如 RPC 服务端、Yggdrasil 服务端的初始化等，均应在此时运行。<br/>
    /// 非异步启动将在 STA 线程执行，不建议在此状态非异步启动。
    /// </summary>
    Loaded,
    
    /// <summary>
    /// <b>窗口创建</b><br/>
    /// 主窗体内容初始化，正常情况下不应有任何与主窗体初始化无关的事件在此时运行。<br/>
    /// 非异步启动将在 STA 线程执行。
    /// </summary>
    WindowCreating,
    
    /// <summary>
    /// <b>窗口创建结束</b><br/>
    /// 一些事件需要依赖已经加载完成的窗体，如初始弹窗提示、主题刷新等，应在此时运行。<br/>
    /// 非异步启动将在 STA 线程执行，耗时操作可能导致主窗体卡顿。
    /// </summary>
    WindowCreated,
    
    /// <summary>
    /// <b>正在运行</b><br/>
    /// 程序开始正常运行后的工作，如检查更新。<br/>
    /// 非异步启动将在新的工作线程执行。
    /// </summary>
    Running,
    
    /// <summary>
    /// <b>尝试关闭程序</b><br/>
    /// 可能有服务需要阻止启动器退出？类似 WPF 窗体的 Closing 事件，但启动器应该没这需求吧...<br/>
    /// 非异步启动将在 STA 线程执行，耗时操作可能导致主窗体卡顿。
    /// </summary>
    Closing,
    
    /// <summary>
    /// <b>关闭程序</b><br/>
    /// 确认关闭程序后开始执行关闭流程，一些需要保存状态的服务项应在此时运行。
    /// 生命周期管理会在此时自动执行所有托管的未停止服务项的 <c>Stop</c> 方法，因此托管的服务项无需额外关注该状态。<br/>
    /// 非异步启动可能在任意线程执行，耗时操作可能导致主窗体卡顿。
    /// </summary>
    Exiting,
}
