using Avalonia.Threading;
using PCL.Core.Platform;

namespace PCL.Core.Avalonia;

/// <summary>
///     基于 Avalonia 调度队列的模态消息泵。
///     Avalonia 没有 WPF 的 <c>DispatcherFrame</c>，这里用 <see cref="Dispatcher.RunJobs" /> 驱动排队任务；
///     原生输入仍由主消息循环处理，待弹窗迁移到 Avalonia 后改为异步语义。
/// </summary>
internal sealed class AvaloniaModalFrame : IUiModalFrame
{
    private volatile bool _exited;

    public void Exit()
    {
        _exited = true;
    }

    public void Run()
    {
        while (!_exited)
        {
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
            Thread.Sleep(1);
        }
    }
}

/// <summary>界面不可用时的空模态消息泵。</summary>
internal sealed class NoopModalFrame : IUiModalFrame
{
    public void Exit()
    {
    }

    public void Run()
    {
    }
}