namespace PCL.Core.App;

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
