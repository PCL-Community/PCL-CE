using System.Threading.Tasks;

namespace PCL.Core.App.IoC;

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
/// 日志服务专用接口。整个生命周期只能有一个日志服务，若出现第二个将会报错。
/// </summary>
public interface ILifecycleLogService : ILifecycleService
{
    /// <summary>
    /// 记录日志的事件
    /// </summary>
    public void OnLog(LifecycleLogItem item);
}
