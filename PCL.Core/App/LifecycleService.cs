using System;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.App;

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
/// 生命周期日志项
/// </summary>
/// <param name="Source">日志来源</param>
/// <param name="Message">日志内容</param>
/// <param name="Exception">相关异常</param>
/// <param name="Level">日志等级</param>
/// <param name="ActionLevel">行为等级</param>
[Serializable]
public record LifecycleLogItem(
    ILifecycleService? Source,
    string Message,
    Exception? Exception,
    LogLevel Level,
    ActionLevel ActionLevel)
{
    /// <summary>
    /// 创建该日志项的时间
    /// </summary>
    public DateTime Time { get; } = DateTime.Now;

    /// <summary>
    /// 创建该日志项的线程名
    /// </summary>
    public string ThreadName { get; } = Thread.CurrentThread.Name ?? $"#{Environment.CurrentManagedThreadId}";

    public override string ToString()
    {
        var source = (Source == null) ? "" : $" [{Source.Name}|{Source.Identifier}]";
        var basic = $"[{Time:HH:mm:ss.fff}]{source}";
        return Exception == null ? $"{basic} {Message}" : $"{basic} ({Message}) {Exception.GetType().FullName}: {Exception.Message}";
    }

    public string ComposeMessage()
    {
        var source = (Source == null) ? "" : $" [{Source.Name}|{Source.Identifier}]";
        var result = $"[{Time:HH:mm:ss.fff}] [{Level.RealLevel().PrintName()}] [{ThreadName}]{source} {Message}";
        if (Exception != null) result += $"\n{Exception}";
        return result;
    }
}

/// <summary>
/// 日志服务专用接口。整个生命周期只能有一个日志服务，若出现第二个将会报错。
/// </summary>
public interface ILifecycleLogService : ILifecycleService
{
    /// <summary>
    /// 记录日志的事件
    /// </summary>
    public void OnLog(LifecycleLogItem item);
}

/// <summary>
/// 注册生命周期服务项，将由生命周期管理统一创建实例，然后在指定生命周期自动启动或加入等待手动启动列表。<br/>
/// 使用此注解的类型必须直接或间接实现 <see cref="ILifecycleService"/> 接口，否则将被忽略。
/// </summary>
/// <param name="startState">详见 <see cref="StartState"/></param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LifecycleServiceAttribute(LifecycleState startState) : Attribute
{
    /// <summary>
    /// 指定该服务项应于何种生命周期状态启动。生命周期管理将在指定的状态按照 <see cref="Priority"/> 自动启动服务项。
    /// </summary>
    public LifecycleState StartState { get; } = startState;

    /// <summary>
    /// 启动优先级。同一个生命周期状态有多个服务项需要启动时，将会按优先级数值<b>降序</b>启动，即数值越大越优先。<br/>
    /// 虽然这个值可以为任意 32 位整数，但是<b>非核心服务请勿使用较为极端的值，尤其是
    /// <c>int.MaxValue</c> <c>int.MinValue</c></b>，这可能导致一些核心服务的启动时机出现问题。
    /// </summary>
    public int Priority { get; init; } = 0;
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

#region Lifecycle Scope Attributes
#pragma warning disable CS9113 // Parameter is unread.

/// <summary>
/// 标记一个 partial 类，自动实现 <see cref="ILifecycleService"/> 接口，并基于其他有标记的方法生成
/// <see cref="ILifecycleService.StartAsync"/> 和 <see cref="ILifecycleService.StopAsync"/> 方法
/// </summary>
/// <param name="identifier">See <see cref="ILifecycleService.Identifier"/></param>
/// <param name="name">See <see cref="ILifecycleService.Name"/></param>
/// <param name="asyncStart">See <see cref="ILifecycleService.SupportAsync"/></param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class LifecycleScopeAttribute(string identifier, string name, bool asyncStart = true) : Attribute;

/// <summary>
/// 标记一个 Start 方法，可以标记多个
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LifecycleStartAttribute : Attribute;

/// <summary>
/// 标记一个 Stop 方法，可以标记多个
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LifecycleStopAttribute : Attribute;

/// <summary>
/// 标记一个参数处理器，可以标记多个
/// </summary>
/// <param name="name">参数名</param>
/// <param name="defaultValue">默认值</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LifecycleArgumentHandlerAttribute<TArgument>(string name, TArgument? defaultValue = default) : Attribute;

#pragma warning restore CS9113 // Parameter is unread.
#endregion
