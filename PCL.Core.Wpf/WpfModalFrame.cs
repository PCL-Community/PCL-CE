using System.Windows.Interop;
using System.Windows.Threading;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF <see cref="DispatcherFrame" /> 的模态消息泵。</summary>
internal sealed class WpfModalFrame : IUiModalFrame
{
    private readonly DispatcherFrame _frame = new(true);
    private bool _exited;

    public void Exit()
    {
        _exited = true;
        _frame.Continue = false;
    }

    public void Run()
    {
        if (_exited || !_frame.Continue) return;

        ComponentDispatcher.PushModal();
        try
        {
            Dispatcher.PushFrame(_frame);
        }
        finally
        {
            ComponentDispatcher.PopModal();
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
