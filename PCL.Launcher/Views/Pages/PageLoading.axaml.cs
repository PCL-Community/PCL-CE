using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;

namespace PCL.Views.Pages;

/// <summary>
///     启动加载页：展示启动阶段状态，进入 Running 状态后由主窗口切换到主页。
///     状态文案按当前语言从本地化资源读取，语言切换后刷新。
/// </summary>
public partial class PageLoading : UserControl
{
    private readonly TextBlock? _statusText;
    private LifecycleState _state = Lifecycle.CurrentState;

    public PageLoading()
    {
        InitializeComponent();
        _statusText = this.FindControl<TextBlock>("StatusText");
        _UpdateStatus(_state);
        Lifecycle.StateChanged += _OnStateChanged;
        LocalizationService.LanguageChanged += _OnLanguageChanged;
        DetachedFromVisualTree += (_, _) =>
        {
            Lifecycle.StateChanged -= _OnStateChanged;
            LocalizationService.LanguageChanged -= _OnLanguageChanged;
        };
    }

    private void _OnStateChanged(LifecycleState state)
    {
        if (Dispatcher.UIThread.CheckAccess())
            _UpdateStatus(state);
        else
            Dispatcher.UIThread.Post(() => _UpdateStatus(state));
    }

    private void _OnLanguageChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
            _UpdateStatus(_state);
        else
            Dispatcher.UIThread.Post(() => _UpdateStatus(_state));
    }

    private void _UpdateStatus(LifecycleState state)
    {
        _state = state;
        if (_statusText is null) return;
        _statusText.Text = Lang.Text(_StatusKey(state));
    }

    private static string _StatusKey(LifecycleState state)
    {
        return state switch
        {
            LifecycleState.BeforeLoading => "Application.Loading.Status.Preparing",
            LifecycleState.Loading or LifecycleState.Loaded => "Application.Loading.Status.Loading",
            LifecycleState.WindowCreating => "Application.Loading.Status.CreatingWindow",
            LifecycleState.WindowCreated => "Application.Loading.Status.InitializingUi",
            LifecycleState.Running => "Application.Loading.Status.Ready",
            LifecycleState.Closing or LifecycleState.Exiting =>
                "Application.Loading.Status.Exiting",
            _ => "Common.State.Unknown"
        };
    }
}