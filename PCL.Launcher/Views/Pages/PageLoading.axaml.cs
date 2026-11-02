using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.IoC;

namespace PCL.Views.Pages;

/// <summary>启动加载页：展示启动阶段状态，进入 Running 状态后由主窗口切换到主页。</summary>
public partial class PageLoading : UserControl
{
    private readonly TextBlock? _statusText;

    public PageLoading()
    {
        InitializeComponent();
        _statusText = this.FindControl<TextBlock>("StatusText");
        _UpdateStatus(Lifecycle.CurrentState);
        Lifecycle.StateChanged += _OnStateChanged;
        DetachedFromVisualTree += (_, _) => Lifecycle.StateChanged -= _OnStateChanged;
    }

    private void _OnStateChanged(LifecycleState state)
    {
        if (Dispatcher.UIThread.CheckAccess())
            _UpdateStatus(state);
        else
            Dispatcher.UIThread.Post(() => _UpdateStatus(state));
    }

    private void _UpdateStatus(LifecycleState state)
    {
        _statusText?.Text = state switch
        {
            LifecycleState.BeforeLoading => "正在准备启动…",
            LifecycleState.Loading or LifecycleState.Loaded => "正在加载基础组件…",
            LifecycleState.WindowCreating => "正在创建主窗口…",
            LifecycleState.WindowCreated => "正在初始化界面…",
            LifecycleState.Running => "就绪",
            LifecycleState.Closing or LifecycleState.Exiting => "正在退出…",
            _ => state.ToString()
        };
    }
}