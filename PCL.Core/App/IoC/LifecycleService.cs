using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.IoC;

partial class Lifecycle
{
    private static readonly ConcurrentDictionary<string, LifecycleServiceInfo> _RunningServiceInfoMap = [];
    private static readonly ConcurrentStack<ILifecycleService> _StartedServiceStack = [];
    private static readonly Dictionary<string, ILifecycleService> _ManualServiceMap = [];
    private static readonly HashSet<ILifecycleService> _DeclaredStoppedServices = [];
    private static readonly ConcurrentDictionary<string, Exception> _ServiceLastExceptionMap = [];

    /// <summary>
    /// 所有正在运行的服务项标识符（即 <see cref="ILifecycleService.Identifier"/> 属性）
    /// </summary>
    public static ICollection<string> RunningServices => _RunningServiceInfoMap.Keys;

    /// <summary>
    /// 服务项启动前触发的事件。<br/>
    /// 该事件会在与服务项初始化相同的线程中执行。请注意，该事件可能在多个不同线程中被同时调用。
    /// </summary>
    public static event Action<string>? ServiceStarting;

    /// <summary>
    /// 服务项启动后触发的事件。<br/>
    /// 该事件会在与服务项初始化相同的线程中执行。请注意，该事件可能在多个不同线程中被同时调用。
    /// </summary>
    public static event Action<string>? ServiceStarted;

    /// <summary>
    /// 服务项停止前触发的事件。特别地，主动声明停止 (<see cref="LifecycleContext.DeclareStopped"/>) 的服务不会触发该事件。<br/>
    /// 该事件会在与服务项停止相同的线程中执行。请注意，该事件可能在多个不同线程中被同时调用。
    /// </summary>
    public static event Action<string>? ServiceStopping;

    /// <summary>
    /// 服务项停止后触发的事件。特别地，主动声明停止 (<see cref="LifecycleContext.DeclareStopped"/>) 的服务不会触发该事件。<br/>
    /// 该事件会在与服务项停止相同的线程中执行。请注意，该事件可能在多个不同线程中被同时调用。
    /// </summary>
    public static event Action<string>? ServiceStopped;

    /// <summary>
    /// 服务项主动声明停止 (<see cref="LifecycleContext.DeclareStopped"/>) 触发的事件。<br/>
    /// 该事件会在与服务项初始化相同的线程中执行。请注意，该事件可能在多个不同线程中被同时调用。
    /// </summary>
    public static event Action<string>? ServiceDeclaredStopped;

    /// <summary>
    /// 服务项抛出异常时触发的事件。<br/>
    /// 该事件会在服务抛出异常的线程中执行。请注意，该事件可能在多个不同线程中被同时调用。
    /// </summary>
    public static event Action<string, Exception>? ServiceUnhandledException;

    private static string _ServiceName(ILifecycleService service, LifecycleState? state = null)
    {
#if DEBUG
        var info = GetServiceInfo(service.Identifier);
        if (info != null) state = info.StartState;
        var stateText = (state == null) ? "" : $"{state}/";
        return $"{service.Name} ({stateText}{service.Identifier})";
#else
        return service.Name;
#endif
    }

    private static Task _StartServiceTask(ILifecycleService service, bool manual = false)
    {
        ILifecycleLogService? logService = null;
        // 检测日志服务
        if (service is ILifecycleLogService ls)
        {
            if (_logService != null) throw new InvalidOperationException("日志服务只能有一个");
            logService = ls;
        }
        var state = manual ? LifecycleState.Manual : CurrentState;
        var name = _ServiceName(service, state);
        // 确保不存在重复的标识符
        lock (_ManualServiceMap) {
            if (_ManualServiceMap.ContainsKey(service.Identifier) && IsServiceRunning(service.Identifier))
            {
                Context.Warn($"{name} 标识符重复，已跳过");
                return Task.CompletedTask;
            }
            // 先找个东西占着防止异步加载中检测逻辑失效
            _RunningServiceInfoMap[service.Identifier] = _SystemServiceInfo;
        }
        // 运行服务项并添加到正在运行列表
        return service.SupportAsync ? Task.Run(AsyncCall) : AsyncCall();
        async Task AsyncCall()
        {
            try
            {
                Context.Trace($"正在启动 {name}");
                ServiceStarting?.Invoke(service.Identifier);
                await service.StartAsync().ConfigureAwait(!service.SupportAsync);
                var serviceInfo = new LifecycleServiceInfo(service, state);
                Context.Debug($"{name} 启动成功");
                if (_DeclaredStoppedServices.Contains(service))
                {
                    _DeclaredStoppedServices.Remove(service);
                    ServiceDeclaredStopped?.Invoke(service.Identifier);
                    Context.Trace($"{name} 已中止");
                }
                else
                {
                    // 若该服务未声明自己已结束运行，将其添加到正在运行列表
                    _StartedServiceStack.Push(service);
                    _RunningServiceInfoMap[service.Identifier] = serviceInfo;
                    ServiceStarted?.Invoke(service.Identifier);
                }
            }
            catch (Exception ex)
            {
                Context.Warn($"{name} 启动失败，尝试停止", ex);
                // 存储异常并停止服务
                _UpdateLastException(service, ex);
                _StopService(service, false);
            }
            // 若日志服务已启动则清空日志缓冲
            if (logService == null) return;
            lock (_PendingLogs)
            {
                foreach (var item in _PendingLogs) _PushLog(item, logService);
                _PendingLogs.Clear();
                _logService = logService;
            }
        }
    }

    private static Type[] _GetServiceTypes(LifecycleState state) => LifecycleServiceTypes.GetServiceTypes(state);

    private static ILifecycleService _CreateService(Type type)
    {
        var fullname = type.FullName;
        try
        {
            SystemContext.Trace($"正在实例化 {fullname}");
            var instance = (ILifecycleService)Activator.CreateInstance(type, true)!;
            var supportAsyncText = instance.SupportAsync ? "异步" : "同步";
            SystemContext.Trace($"实例化完成: {instance.Name} ({instance.Identifier}), 启动方式: {supportAsyncText}");
            return instance;
        }
        catch (Exception ex)
        {
            SystemContext.Fatal($"注册服务项实例化失败: {fullname}", ex);
            throw;
        }
    }

    private static void _LogStateCount(TimeSpan count, LifecycleState state)
    {
        Context.Debug($"状态 {state} 共用时 {Math.Round(count.TotalMilliseconds)} ms");
    }

    private static void _InitializeAndStartStateServices(LifecycleState state)
    {
        var types = _GetServiceTypes(state);
        if (types.Length == 0) return; // 跳过空列表
        var asyncInstances = new List<ILifecycleService>();
        // 运行非异步启动服务并存储异步启动服务
        foreach (var service in types)
        {
            var instance = _CreateService(service);
            if (instance.SupportAsync) asyncInstances.Add(instance);
            else _StartServiceTask(instance).ConfigureAwait(false).GetAwaiter().GetResult();
            if (_hasRequestedStopLoading) return; // 若请求停止加载则提前结束
        }
        // 运行异步启动服务并等待所有服务启动完成
        Task.WaitAll(asyncInstances.Select(instance => _StartServiceTask(instance)).ToArray());
    }

    private static void _StartStateFlow(LifecycleState start, LifecycleState? end = null, bool count = true)
    {
        var index = (int)start;
        var endIndex = end == null ? index : (int)end;
        while (index <= endIndex)
        {
            DateTime? countStart = count ? DateTime.Now : null; //开始计时
            var state = (LifecycleState)index;
            _NextState(state);
            _InitializeAndStartStateServices(state);
            if (countStart is { } s)
            {
                var countSpan = DateTime.Now - s; // 结束计时
                _LogStateCount(countSpan, state);
            }
            index++;
        }
    }

    private static void _StartWorker(LifecycleState state, LifecycleState? wait = null, bool count = true)
    {
        new Thread(() =>
        {
            _StartStateFlow(state, count: count);
            if (wait is { } w) WaitForState(w);
        })
        { IsBackground = true, Name = $"Lifecycle/{state}" }.Start();
    }

    private static void _RemoveRunningInstance(ILifecycleService service)
    {
        _RunningServiceInfoMap.TryRemove(service.Identifier, out var removed);
        removed?.MarkAsStopped();
    }

    private static readonly ConcurrentBag<Task> _StoppingServiceTasks = [];

    private static void _WaitStoppingServiceTasks()
    {
        Task.WaitAll(_StoppingServiceTasks.ToArray());
    }

    private static void _StopService(ILifecycleService service, bool async, bool manual = false)
    {
        var name = _ServiceName(service, manual ? LifecycleState.Manual : CurrentState);
        if (async) _StoppingServiceTasks.Add(Task.Run(Stop));
        else Stop().ConfigureAwait(false).GetAwaiter().GetResult();
        return;

        async Task Stop()
        {
            try
            {
                Context.Trace($"正在停止 {name}");
                ServiceStopping?.Invoke(service.Identifier);
                await service.StopAsync().ConfigureAwait(!async);
                ServiceStopped?.Invoke(service.Identifier);
                Context.Debug($"{name} 已停止");
            }
            catch (Exception ex)
            {
                // 若出错则存储异常并忽略
                Context.Warn($"停止 {name} 时出错，已跳过", ex);
                _UpdateLastException(service, ex);
            }
            // 从正在运行列表移除
            _RemoveRunningInstance(service);
        }
    }

    private static void _UpdateLastException(ILifecycleService service, Exception ex)
    {
        _ServiceLastExceptionMap[service.Identifier] = ex;
        ServiceUnhandledException?.Invoke(service.Identifier, ex);
    }

    /// <summary>
    /// 检查指定标识符的服务项是否正在运行
    /// </summary>
    /// <param name="identifier">服务项标识符（即 <see cref="ILifecycleService.Identifier"/> 属性）</param>
    /// <returns>服务项是否正在运行</returns>
    public static bool IsServiceRunning(string identifier) => _RunningServiceInfoMap.ContainsKey(identifier);

    /// <summary>
    /// 根据标识符获取正在运行的服务项的相关信息
    /// </summary>
    /// <param name="identifier">服务项标识符（即 <see cref="ILifecycleService.Identifier"/> 属性）</param>
    /// <returns>服务项信息</returns>
    public static LifecycleServiceInfo? GetServiceInfo(string? identifier)
    {
        if (identifier == null) return null;
        _RunningServiceInfoMap.TryGetValue(identifier, out var info);
        return info;
    }

    /// <summary>
    /// 获取服务项初始化或结束逻辑的最后一次异常。
    /// </summary>
    /// <param name="identifier">服务项标识符（即 <see cref="ILifecycleService.Identifier"/> 属性）</param>
    /// <returns>异常实例，若未出现异常则为 <c>null</c></returns>
    public static Exception? GetServiceLastException(string identifier)
    {
        var result = _ServiceLastExceptionMap.TryGetValue(identifier, out var ex);
        return result ? ex : null;
    }

    /// <summary>
    /// 手动请求启动一个周期为 <see cref="LifecycleState.Manual"/> 的服务项。
    /// </summary>
    /// <param name="identifier">服务项标识符（即 <see cref="ILifecycleService.Identifier"/> 属性）</param>
    /// <param name="async">是否异步启动，默认遵循服务项自身的声明</param>
    /// <returns>是否成功请求启动，若该服务项正在运行或周期不是 <see cref="LifecycleState.Manual"/> 则无法启动</returns>
    public static bool StartService(string identifier, bool? async = null)
    {
        _ManualServiceMap.TryGetValue(identifier, out var service);
        if (service == null || IsServiceRunning(identifier)) return false;
        async ??= service.SupportAsync;
        if (async == true) Task.Run(() => _StartServiceTask(service, true));
        else _StartServiceTask(service, true);
        return true;
    }

    /// <summary>
    /// 手动请求停止一个周期为 <see cref="LifecycleState.Manual"/> 的服务项。
    /// </summary>
    /// <param name="identifier">服务项标识符（即 <see cref="ILifecycleService.Identifier"/> 属性）</param>
    /// <param name="async">是否异步停止，默认为 <c>true</c></param>
    /// <returns>是否成功请求停止，若该服务项未运行或周期不是 <see cref="LifecycleState.Manual"/> 则无法停止</returns>
    public static bool StopService(string identifier, bool async = true)
    {
        _ManualServiceMap.TryGetValue(identifier, out var service);
        if (service == null || !IsServiceRunning(identifier)) return false;
        _StopService(service, async, true);
        return true;
    }

    /// <summary>
    /// 运行自定义服务项。该服务项将使用当前生命周期状态作为启动状态，若无特殊需求请尽可能不要使用，而是直接注册服务项。
    /// </summary>
    /// <param name="service">服务项实例</param>
    /// <returns>是否成功请求运行，若标识符与正在运行的服务或已注册的手动服务冲突则无法运行。</returns>
    public static bool StartCustomService(ILifecycleService service)
    {
        if (IsServiceRunning(service.Identifier) || _ManualServiceMap.ContainsKey(service.Identifier)) return false;
        _StartServiceTask(service);
        return true;
    }
}

/// <summary>
/// 用于特定生命周期的服务模型。<br/>
/// 实现特殊的子接口 <see cref="ILifecycleLogService"/> 以声明自己是日志服务。
/// </summary>
public interface ILifecycleService
{
    /// <summary>
    /// 全局唯一标识符，统一使用纯小写字母与 “-” 的命名格式，如 <c>logger</c> <c>yggdrasil-server</c> 等。
    /// </summary>
    public string Identifier { get; }
    
    /// <summary>
    /// 友好名称，如 “日志” “验证服务端” 等，将会用于记录日志等场合。
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// 声明该服务是否支持异步启动。
    /// 每个生命周期均会依次同步启动不支持异步启动的服务，然后依次异步启动支持异步启动的服务，启动的执行顺序遵循声明的优先级。<br/>
    /// 支持异步启动对启动器整体启动速度有一定帮助，在允许的情况下应尽最大可能支持。
    /// </summary>
    public bool SupportAsync { get; }
    
    /// <summary>
    /// 启动该服务。应由生命周期管理自动调用，若无特殊情况，请勿手动调用。
    /// </summary>
    public Task StartAsync();

    /// <summary>
    /// 停止该服务。应由生命周期管理自动调用，若无特殊情况，请勿手动调用。
    /// </summary>
    public Task StopAsync();
}

/// <summary>
/// 生命周期服务项的信息记录
/// </summary>
public record LifecycleServiceInfo
{
    private readonly ILifecycleService _service;
    public string Identifier => _service.Identifier;
    public string Name => _service.Name;
    public bool CanStartAsync => _service.SupportAsync;
    public LifecycleState StartState { get; }

    /// <summary>
    /// 服务开始运行的时间。初始值为调用 <c>Start()</c> 方法的时刻，在 <c>Start()</c> 方法结束之后会更新一次。
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.Now;

    /// <summary>
    /// 附带启动状态的完整标识符。
    /// </summary>
    public string FullIdentifier => $"{StartState}/{Identifier}";

    /// <summary>
    /// 服务是否正常运行，若已停止则该值为 <c>false</c>，否则为 <c>true</c>。
    /// </summary>
    public bool IsStopped { get; private set; } = false;

    /// <summary>
    /// 将该服务标记为已停止，将不会在程序退出流程中调用该服务的 <c>Stop()</c> 方法。
    /// </summary>
    public void MarkAsStopped() => IsStopped = true;

    /// <summary>
    /// 本 record 应由生命周期管理自动构造，若无特殊情况，请勿手动调用。
    /// </summary>
    /// <param name="service">生命周期服务项实例</param>
    /// <param name="startState">启动的生命周期状态</param>
    public LifecycleServiceInfo(ILifecycleService service, LifecycleState startState)
    {
        _service = service;
        StartState = startState;
        StartTime = DateTime.Now;
    }
}
