using System;

namespace PCL.Core.App.Configuration.NTraffic;

/// <summary>
/// 同步物流中心，提供同步执行所有操作的消费实现。
/// </summary>
public abstract class SyncTrafficCenter : TrafficCenter
{
    protected sealed override void OnTraffic<TInput, TOutput>(
        PreviewTrafficEventArgs<TInput, TOutput> e,
        Action<PreviewTrafficEventArgs<TInput, TOutput>> onInvokeEvent)
    {
        OnTrafficSync(e);
        onInvokeEvent(e);
    }

    protected virtual void OnTrafficSync<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e) { }

    protected override void OnStop() { }
}
