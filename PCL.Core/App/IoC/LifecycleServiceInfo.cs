using System;

namespace PCL.Core.App.IoC;

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
