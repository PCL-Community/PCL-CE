using System;
using System.Threading.Tasks;
using PCL.Core.Utils.Threading;

namespace PCL.Core.App.Configuration.NTraffic;

/// <summary>
/// 异步物流中心，提供可选异步执行操作的消费实现。<br/>
/// 注意：异步物流将忽略输出值。
/// </summary>
public abstract class AsyncTrafficCenter(int maxThread) : TrafficCenter
{
    private readonly LimitedTaskPool _taskPool = new(maxThread);

    protected sealed override void OnTraffic<TInput, TOutput>(
        PreviewTrafficEventArgs<TInput, TOutput> e,
        Action<PreviewTrafficEventArgs<TInput, TOutput>> onInvokeEvent)
    {
        if (OnAsyncCheck(e)) _taskPool.Submit(async () =>
        {
            await OnTrafficAsync(e).ConfigureAwait(false);
            onInvokeEvent(e);
        });
        else
        {
            OnTrafficSync(e);
            onInvokeEvent(e);
        }
    }

    protected override void OnStop() { }

    protected virtual void OnTrafficSync<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e) { }

    protected virtual Task OnTrafficAsync<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e) => Task.CompletedTask;

    protected virtual bool OnAsyncCheck<TInput, TOutput>(TrafficEventArgs<TInput, TOutput> e) => true;
}
