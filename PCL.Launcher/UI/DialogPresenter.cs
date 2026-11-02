using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PCL.Core.UI.Dialogs;
using PCL.UI.Animations;
using PCL.UI.Dialogs;

namespace PCL.UI;

/// <summary>
///     把 Core 的弹窗状态按类型渲染为对应弹窗控件，并在主窗口的遮罩层中呈现：
///     遮罩与弹窗一起淡入，关闭时先淡出再移除视图并释放遮罩。
///     主窗口或遮罩层尚未就绪时退回独立窗口，界面完全不可用时退回无主窗口的独立窗口。
/// </summary>
public sealed class DialogPresenter : IDialogPresenter
{
    /// <summary>遮罩层淡入的基准时长（毫秒），实际时长按动画速度折算。</summary>
    private const int FadeInMilliseconds = 120;

    /// <summary>遮罩层淡出的基准时长（毫秒），实际时长按动画速度折算。</summary>
    private const int FadeOutMilliseconds = 150;

    /// <summary>遮罩层内承载弹窗视图的容器名称。</summary>
    private const string DialogHostName = "PanDialogHost";

    /// <summary>遮罩层淡入淡出的动画组名；重新淡入时先停掉尚未结束的淡出。</summary>
    private const string LayerFadeKey = "Dialog Layer Fade";

    /// <summary>遮罩层内按显示顺序堆叠的弹窗，后加入的盖在之前的上面。</summary>
    private readonly List<OverlayDialog> _dialogs = [];

    /// <summary>走独立窗口的弹窗，仅在主窗口或遮罩层不可用时使用。</summary>
    private readonly Dictionary<DialogViewModel, Window> _windows = [];

    /// <summary>已关闭、等淡出结束后才从容器移除的弹窗。</summary>
    private OverlayDialog? _closing;

    private Panel? _host;
    private Panel? _layer;

    /// <summary>淡出动画未按时结束时的兜底定时器，防止遮罩一直挡住界面。</summary>
    private DispatcherTimer? _releaseTimer;

    /// <summary>最后一个弹窗正在淡出，期间 <see cref="IsAvailable" /> 保持为 <see langword="false" />。</summary>
    private bool _releasing;

    private Window? _window;

    public static DialogPresenter Instance { get; } = new();

    /// <summary>
    ///     主窗口可见、未被最小化，且没有弹窗正在显示或淡出时才可呈现；
    ///     否则返回 <see langword="false" />，由 <see cref="DialogService" /> 排队重试或退回兜底。
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            var window = _window ?? _GetMainWindow();
            return window is { IsVisible: true }
                && window.WindowState != WindowState.Minimized
                && _windows.Count == 0
                && _dialogs.Count == 0
                && !_releasing;
        }
    }

    /// <summary>在主窗口的遮罩层中央显示弹窗；遮罩层不可用时退回独立窗口。</summary>
    public void Present(DialogViewModel dialog)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Present(dialog));
            return;
        }

        if (dialog.IsClosed) return;

        var owner = _window ?? _GetMainWindow();
        if (owner is null)
        {
            ShowFallback(dialog);
            return;
        }

        if (_layer is { } layer && _host is { } host)
            try
            {
                _PresentInLayer(layer, host, dialog);
                return;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UI] 弹窗显示失败：{ex}");
                _Discard(dialog);
                dialog.Close(DialogResult.None);
                return;
            }

        _PresentInWindow(owner, dialog);
    }

    /// <summary>移除弹窗：遮罩层内的弹窗做淡出，独立窗口直接关闭。</summary>
    public void Dismiss(DialogViewModel dialog)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Dismiss(dialog));
            return;
        }

        _DismissFromLayer(dialog);
        _CloseWindow(dialog);
    }

    /// <summary>界面不可用时的兜底呈现：尽量仍以无主窗口的独立窗口显示，最终一定关闭弹窗。</summary>
    public void ShowFallback(DialogViewModel dialog)
    {
        if (dialog.IsClosed) return;

        if (Application.Current is null)
        {
            Console.Error.WriteLine($"[UI] [{dialog.Title}] {dialog.TextContent}");
            dialog.Close(DialogResult.None);
            return;
        }

        Window? window = null;
        try
        {
            window = _BuildWindow(dialog);
            _windows[dialog] = window;
            AttachWindow(window, dialog);
            window.Show();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UI] 弹窗兜底显示失败：{ex}");
            ReleaseWindow(window, dialog);
            dialog.Close(DialogResult.None);
        }
    }

    public static void Install()
    {
        DialogService.Instance.Presenter = Instance;
    }

    /// <summary>绑定主窗口及其遮罩层，由主窗口构造时调用；容器按名称在窗口内查找。</summary>
    public void Attach(Window window, Panel layer)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Attach(window, layer));
            return;
        }

        if (ReferenceEquals(_window, window) && ReferenceEquals(_layer, layer)) return;

        _Detach();
        _window = window;
        _layer = layer;
        _host = window.FindControl<Panel>(DialogHostName)
            ?? layer.FindControl<Panel>(DialogHostName);

        // 遮罩显示期间 Tab 只在遮罩层内循环，焦点不会被底层页面接走
        KeyboardNavigation.SetTabNavigation(layer, KeyboardNavigationMode.Cycle);

        window.Closed += _OnMainWindowClosed;
    }

    private static MyMsgDialogBase CreateDialogView(DialogViewModel dialog)
    {
        return dialog.Kind switch
        {
            DialogKind.Text => new MyMsgText(dialog),
            DialogKind.Markdown => new MyMsgMarkdown(dialog),
            DialogKind.Input => new MyMsgInput(dialog),
            DialogKind.Select => new MyMsgSelect(dialog),
            DialogKind.AuthServer => new MyMsgAuthServer(dialog),
            DialogKind.Login => new MyMsgLogin(dialog),
            _ => new MyMsgText(dialog)
        };
    }

    private static Window? _GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            lifetime)
            return lifetime.MainWindow;
        return null;
    }

    /// <summary>遮罩层内的一个弹窗：状态、渲染控件与事件订阅，关闭时转交呈现器收尾。</summary>
    private sealed class OverlayDialog(
        DialogViewModel dialog,
        MyMsgDialogBase view,
        Action<DialogViewModel> dismiss)
    {
        public DialogViewModel Dialog { get; } = dialog;

        public MyMsgDialogBase View { get; } = view;

        private bool Subscribed { get; set; }

        public void Attach()
        {
            if (Subscribed) return;
            Subscribed = true;
            Dialog.PropertyChanged += OnDialogPropertyChanged;
            View.Loaded += OnViewLoaded;
        }

        public void Detach()
        {
            if (!Subscribed) return;
            Subscribed = false;
            Dialog.PropertyChanged -= OnDialogPropertyChanged;
            View.Loaded -= OnViewLoaded;
        }

        private void OnDialogPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DialogViewModel.IsClosed) || !Dialog.IsClosed) return;

            // 关闭可能来自后台线程，统一回界面线程做淡出与移除
            Dispatcher.UIThread.Post(() => dismiss(Dialog));
        }

        private void OnViewLoaded(object? sender, RoutedEventArgs e)
        {
            View.Loaded -= OnViewLoaded;
            if (View.Parent is null) return;

            // 视图此时已随遮罩显示，先直接取焦点；尚未完成布局时留到下一次布局再取
            if (!View.FocusInitial())
                Dispatcher.UIThread.Post(FocusIfAttached, DispatcherPriority.Loaded);
        }

        private void FocusIfAttached()
        {
            if (View.Parent is null || !View.IsEffectivelyVisible) return;

            View.FocusInitial();
        }
    }

    #region 遮罩层

    /// <summary>把新建的弹窗控件放到容器最上层，显示遮罩并淡入。</summary>
    private void _PresentInLayer(Panel layer, Panel host, DialogViewModel dialog)
    {
        // 上一次淡出还没走完就来了新弹窗：先把待移除的视图收掉
        if (_closing is not null) _FinishClosing();

        var view = CreateDialogView(dialog);
        var entry = new OverlayDialog(dialog, view, Dismiss);
        _dialogs.Add(entry);
        entry.Attach();

        // 先显示遮罩再挂视图，视图挂上后即处于可见状态，可以正常取得焦点
        layer.IsVisible = true;
        _releasing = false;
        host.Children.Add(view);

        _FadeLayer(layer, 1d, FadeInMilliseconds, () => layer.Opacity = 1d);
    }

    /// <summary>移除遮罩层内的弹窗；最后一个弹窗移除时遮罩淡出，结束后隐藏遮罩层。</summary>
    private void _DismissFromLayer(DialogViewModel dialog)
    {
        var entry = _dialogs.FirstOrDefault(item => ReferenceEquals(item.Dialog, dialog));
        if (entry is null) return;

        _dialogs.Remove(entry);
        entry.Detach();

        // 还有弹窗盖在遮罩上，遮罩保持显示，直接移除这一层
        if (_dialogs.Count > 0)
        {
            _RemoveFromLayer(entry);
            return;
        }

        _closing = entry;
        _releasing = true;
        if (_layer is { } layer)
            _FadeLayer(layer, 0d, FadeOutMilliseconds, _FinishClosing);
        else
            _FinishClosing();

        // 窗口最小化等情况下渲染帧可能停摆，淡出动画收不到回调，用定时器保证遮罩最终释放
        if (_closing is not null)
            _StartReleaseTimer(_GetFadeDuration(FadeOutMilliseconds) + 100);
    }

    private void _StartReleaseTimer(int milliseconds)
    {
        _StopReleaseTimer();

        _releaseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(1, milliseconds))
        };
        _releaseTimer.Tick += _OnReleaseTimerTick;
        _releaseTimer.Start();
    }

    private void _StopReleaseTimer()
    {
        if (_releaseTimer is null) return;

        _releaseTimer.Stop();
        _releaseTimer.Tick -= _OnReleaseTimerTick;
        _releaseTimer = null;
    }

    private void _OnReleaseTimerTick(object? sender, EventArgs e)
    {
        _FinishClosing();
    }

    /// <summary>淡出结束后的收尾：移除视图，没有新弹窗时隐藏并复位遮罩层。</summary>
    private void _FinishClosing()
    {
        _StopReleaseTimer();

        var closing = _closing;
        _closing = null;
        if (closing is not null) _RemoveFromLayer(closing);

        if (_dialogs.Count > 0 || !_releasing) return;

        _releasing = false;
        if (_layer is not { } layer) return;

        PCL.Animations.AniStop(LayerFadeKey);
        layer.IsVisible = false;
        layer.Opacity = 0d;
    }

    /// <summary>把显示失败的弹窗从容器里摘掉，不留残余视图和事件订阅。</summary>
    private void _Discard(DialogViewModel dialog)
    {
        var entry = _dialogs.FirstOrDefault(item => ReferenceEquals(item.Dialog, dialog));
        if (entry is null) return;

        _dialogs.Remove(entry);
        entry.Detach();
        _RemoveFromLayer(entry);

        // 挂载失败的弹窗可能已经把遮罩显示出来，容器空了就一并复位，避免遮罩一直挡住界面
        if (_dialogs.Count > 0 || _layer is not { } layer) return;

        PCL.Animations.AniStop(LayerFadeKey);
        layer.IsVisible = false;
        layer.Opacity = 0d;
        _releasing = false;
    }

    private static void _RemoveFromLayer(OverlayDialog entry)
    {
        if (entry.View.Parent is Panel parent)
            parent.Children.Remove(entry.View);
    }

    /// <summary>按目标透明度播放遮罩层淡入淡出；动画关闭或控件未接入视觉树时直接跳到终值。</summary>
    private void _FadeLayer(
        Panel layer,
        double target,
        int baseMilliseconds,
        Action? completed = null)
    {
        PCL.Animations.AniStop(LayerFadeKey);

        var duration = _GetFadeDuration(baseMilliseconds);
        if (duration <= 0
            || !_CanAnimate(layer)
            || Math.Abs(layer.Opacity - target) <= 0.001d)
        {
            layer.Opacity = target;
            completed?.Invoke();
            return;
        }

        var animations = new List<PCL.Animations.AniData>
        {
            PCL.Animations.AaOpacity(
                layer,
                target - layer.Opacity,
                duration,
                ease: new PCL.Animations.AniEaseOutFluent())
        };
        if (completed is not null)
            animations.Add(PCL.Animations.AaCode(completed, after: true));

        PCL.Animations.AniStart(animations, LayerFadeKey);
    }

    private static int _GetFadeDuration(int baseMilliseconds)
    {
        return !AnimationService.IsEnabled
            ? 0
            : Math.Max(1, (int)Math.Round(baseMilliseconds / AnimationService.Speed));
    }

    private static bool _CanAnimate(Control control)
    {
        return AnimationService.IsEnabled
            && control.IsLoaded
            && TopLevel.GetTopLevel(control) is not null;
    }

    private void _OnMainWindowClosed(object? sender, EventArgs e)
    {
        // 主窗口关闭后遮罩再没有落点：结束淡出动画，弹窗按取消收尾，避免调用方一直等待结果
        _StopReleaseTimer();
        PCL.Animations.AniStop(LayerFadeKey);

        var dialogs = _dialogs.ToArray();
        _dialogs.Clear();
        _closing = null;
        _releasing = false;

        foreach (var entry in dialogs)
        {
            entry.Detach();
            _RemoveFromLayer(entry);
            entry.Dialog.Close(DialogResult.None);
        }

        if (_layer is { } layer)
        {
            layer.IsVisible = false;
            layer.Opacity = 0d;
        }

        _Detach();
    }

    private void _Detach()
    {
        _StopReleaseTimer();

        if (_window is not null)
            _window.Closed -= _OnMainWindowClosed;

        _window = null;
        _layer = null;
        _host = null;
    }

    #endregion

    #region 独立窗口

    /// <summary>主窗口存在但遮罩层未接入时，按原样以主窗口为所有者显示独立窗口。</summary>
    private void _PresentInWindow(Window owner, DialogViewModel dialog)
    {
        Window? window = null;
        try
        {
            window = _BuildWindow(dialog);
            _windows[dialog] = window;
            AttachWindow(window, dialog);
            window.Show(owner);
            window.Activate();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UI] 弹窗显示失败：{ex}");
            ReleaseWindow(window, dialog);
            dialog.Close(DialogResult.None);
        }
    }

    private void AttachWindow(Window window, DialogViewModel dialog)
    {
        window.Closed += (_, _) => _OnDialogWindowClosed(dialog);
        dialog.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DialogViewModel.IsClosed) && dialog.IsClosed)
                Dispatcher.UIThread.Post(() => _CloseWindow(dialog));
        };
    }

    private void ReleaseWindow(Window? window, DialogViewModel dialog)
    {
        _windows.Remove(dialog);
        if (window is null) return;
        try
        {
            window.Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UI] 关闭弹窗失败：{ex}");
        }
    }

    private void _OnDialogWindowClosed(DialogViewModel dialog)
    {
        _windows.Remove(dialog);
        if (dialog.IsClosed) return;

        // 用户直接关闭窗口时视为取消
        dialog.Close(dialog.Buttons.Length > 1 ? DialogResult.Cancel : DialogResult.None);
    }

    private void _CloseWindow(DialogViewModel dialog)
    {
        if (!_windows.Remove(dialog, out var window)) return;
        if (!window.IsVisible) return;
        try
        {
            window.Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UI] 关闭弹窗失败：{ex}");
        }
    }

    /// <summary>独立窗口：固定宽度、按内容高度自适应，主窗口或遮罩层不可用时使用。</summary>
    private static Window _BuildWindow(DialogViewModel dialog)
    {
        var window = new Window
        {
            Title = dialog.Title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 640,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        DialogTheme.BindBrush(
            window,
            TemplatedControl.BackgroundProperty,
            "PageBrushWindowFallback");

        var view = CreateDialogView(dialog);
        window.Content = view;
        window.Opened += (_, _) => view.FocusInitial();
        return window;
    }

    #endregion
}