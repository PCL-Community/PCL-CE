using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PCL.Core.App.IoC;

partial class Lifecycle
{
    private static DateTime? _countRunningStart;

    private static bool _isApplicationStarted = false;
    private static bool _isLoadingStarted = false;
    private static bool _isWindowCreated = false;

    private static bool _hasRequestedStopLoading = false;

    /// <summary>
    /// [请勿调用] 处理未捕获异常流程
    /// </summary>
    /// <param name="ex">异常对象</param>
    public static void OnException(object ex)
    {
        Context.Fatal("未捕获的异常", ex as Exception);
    }

    /// <summary>
    /// [请勿调用] 程序初始化流程
    /// </summary>
    public static void OnInitialize()
    {
        // 检测重复调用
        if (_isApplicationStarted) return;
        _isApplicationStarted = true;
        // 修改 STA 线程名
        Thread.CurrentThread.Name = "STA";
        // 注册全局事件
        AppDomain.CurrentDomain.UnhandledException += (_, e) => OnException(e.ExceptionObject);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _Exit();
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Context.Error("未观测到的异步任务异常", e.Exception);
            e.SetObserved();
        };
        // 添加系统服务
        _RunningServiceInfoMap["system"] = _SystemServiceInfo;
        // 实例化并存储手动服务
        foreach (var service in _GetServiceTypes(LifecycleState.Manual))
        {
            var instance = _CreateService(service);
            var identifier = instance.Identifier;
            if (_ManualServiceMap.TryAdd(identifier, instance)) continue;
            Context.Warn($"{_ServiceName(instance, LifecycleState.Manual)} 标识符重复，已跳过");
        }
        // 运行预加载服务
        _StartStateFlow(LifecycleState.BeforeLoading);
        if (_hasRequestedStopLoading) return;
        // 运行应用程序容器
        var statusCode = CurrentApplication.Run();
        if (!HasShutdownStarted) _Exit(statusCode);
    }

    /// <summary>
    /// [请勿调用] 组件加载流程
    /// </summary>
    public static void OnLoading()
    {
        // 检测重复调用
        if (_isLoadingStarted) return;
        _isLoadingStarted = true;
        // 运行加载阶段服务
        _StartStateFlow(LifecycleState.Loading, LifecycleState.WindowCreating);
        // 运行窗体
        CurrentApplication.MainWindow!.Show();
    }

    /// <summary>
    /// [请勿调用] 窗口创建结束流程
    /// </summary>
    public static void OnWindowCreated()
    {
        // 检测重复调用
        if (_isWindowCreated) return;
        _isWindowCreated = true;
        // 启动窗口流程后的服务项
        _StartStateFlow(LifecycleState.WindowCreated);
        _countRunningStart = DateTime.Now;
        _StartWorker(LifecycleState.Running, LifecycleState.Exiting, false);
    }

    private static bool _hasRequestedRestart = false;
    private static string? _requestRestartArguments;
    private static ILifecycleService? _requestRestartService;

    private static readonly object _ExitLock = new();

    /// <summary>
    /// WPF 应用程序容器，在 <see cref="LifecycleState.BeforeLoading"/> 阶段为空值
    /// </summary>
    public static Application CurrentApplication { get; set; } = null!;

    /// <summary>
    /// 是否正在关闭程序
    /// </summary>
    public static bool HasShutdownStarted { get; private set; } = false;

    /// <summary>
    /// 正在进行的关闭程序流程是否是强制关闭
    /// </summary>
    public static bool IsForceShutdown { get; private set; } = false;

    private static void _FatalExit()
    {
        ForceShutdown(-1);
    }

    private static void _Exit(int statusCode = 0)
    {
        lock (_ExitLock)
        {
            if (HasShutdownStarted) return;
            HasShutdownStarted = true;
        }
        // 结束 Running 计时
        if (_countRunningStart is { } start)
        {
            var countSpan = DateTime.Now - start;
            _LogStateCount(countSpan, LifecycleState.Running);
        }
        // 开始 Exiting 状态
        _StartStateFlow(LifecycleState.Exiting, count: false);
        // 停止服务
        Context.Debug("正在停止运行中的服务");
        ILifecycleLogService? logService = null;
        while (_StartedServiceStack.TryPop(out var service))
        {
            // 跳过已标记为停止的服务
            if (_RunningServiceInfoMap.TryGetValue(service.Identifier, out var info) && info.IsStopped) continue;
            // 跳过日志服务
            if (service is ILifecycleLogService ls)
            {
                Context.Trace($"已跳过日志服务: {_ServiceName(ls)}");
                logService = ls;
                continue;
            }
            // 执行停止流程
            _StopService(service, service.SupportAsync);
        }
        _WaitStoppingServiceTasks();
        if (logService != null)
        {
            Context.Trace("退出过程已结束，正在停止日志服务");
            // 直接调用 StopAsync() 不使用常规停止实现 以保证正常情况下不会向等待区输出日志
            logService.StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            Console.WriteLine("[Lifecycle] Log service stopped");
        }
        _SavePendingLogs();
        if (_hasRequestedRestart && _requestRestartService is { } s)
        {
            Console.WriteLine($"[Lifecycle] Requested by '{s.Identifier}', restarting the program...");
            _RunCurrentExecutable(_requestRestartArguments);
        }
        // 退出程序
        Console.WriteLine($"[Lifecycle] Exiting program with status: {statusCode}");
        // 执行正常退出
        if (statusCode == -1) Basics.CurrentProcess.Kill();
        else Environment.Exit(statusCode);
        // 保险起见，只要运行环境正常根本不可能执行到这里，但是永远都不能假设用户的环境是正常的
        Console.WriteLine("[Lifecycle] Warning! Abnormal behaviour, try to kill process 1s later.");
        Thread.Sleep(1000);
        _KillCurrentProcess();
    }

    /// <summary>
    /// 发起关闭程序流程。<br/>
    /// <see cref="LifecycleState.BeforeLoading"/> 状态请使用 <see cref="LifecycleContext.RequestExit"/>。
    /// </summary>
    /// <param name="statusCode">退出状态码 (返回值)</param>
    /// <param name="force">指定是否强制关闭，即不执行 WPF 标准关闭流程</param>
    /// <exception cref="InvalidOperationException">尝试在 <see cref="LifecycleState.BeforeLoading"/> 状态调用</exception>
    public static void Shutdown(int statusCode = 0, bool force = false)
    {
        if (CurrentState == LifecycleState.BeforeLoading) throw new InvalidOperationException();
        if (HasShutdownStarted) return;
        Context.Info(force ? "开始强制关闭程序" : "正在关闭程序");
        IsForceShutdown = force;
        if (force) _Exit(statusCode);
        else new Thread(() => _Exit(statusCode)) { Name = "Lifecycle/Shutdown" }.Start();
    }

    /// <summary>
    /// 强制关闭程序，不执行 WPF 标准关闭流程。
    /// </summary>
    /// <param name="statusCode">退出状态码 (返回值)</param>
    /// <exception cref="InvalidOperationException">尝试在 <see cref="LifecycleState.BeforeLoading"/> 时调用</exception>
    public static void ForceShutdown(int statusCode = 0) => Shutdown(statusCode, true);
}
