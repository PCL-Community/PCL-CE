using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.Platform;
using PCL.Platform;
using PCL.Views.Shell;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 滚动容器。滚轮按 300 ms 减速动画改变垂直偏移（macOS采取直接跟手触控板的形式），<see cref="DeltaMult" /> 为倍率；
///     模板见 <c>UI/Styles/Controls/ScrollBars.axaml</c>，垂直与水平滚动条均为 <see cref="MyScrollBar" />。
/// </summary>
public class MyScrollViewer : ScrollViewer
{
    private const double WheelScrollStep = 120d; // 一个滚轮刻度对应的像素数
    private const int WheelAnimationTime = 300;
    private const int GestureSafetyTime = 3000; // 漏收手指抬起事件时的兜底回弹时间（单位是ms）

    private static WeakReference<MyScrollViewer>? _activePreciseViewer;
    private static bool? _scrollPhaseMonitorReady;

    public static readonly StyledProperty<Thickness> ScrollBarMarginProperty =
        AvaloniaProperty.Register<MyScrollViewer, Thickness>(nameof(ScrollBarMargin));

    private double _realOffset;
    private WindowBase? _window;
    private bool _isRestoringFocus;
    private MyScrollBar? _verticalScrollBar;
    private MyScrollBar? _horizontalScrollBar;
    private readonly ElasticOverscroll _Overscroll;
    private readonly DispatcherTimer _GestureSafetyTimer;
    private bool _momentumBounced;
    private ulong _lastPreciseTimestamp;
    private int _wheelAnimationGeneration;

    public MyScrollViewer()
    {
        ContainerTheme.AttachStyles(this);
        _Overscroll = new ElasticOverscroll(this);
        _GestureSafetyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GestureSafetyTime) };
        _GestureSafetyTimer.Tick += (_, _) =>
        {
            _GestureSafetyTimer.Stop();
            _Overscroll.Release();
        };

        // 隧道阶段处理滚轮，抢在内容控件之前
        AddHandler(PointerWheelChangedEvent, OnPreviewPointerWheel, RoutingStrategies.Tunnel);
        ScrollChanged += OnScrollChanged;

        PropertyChanged += (_, change) =>
        {
            if (change.Property == IsVisibleProperty)
                NotifyBackButtonRefresh();
        };
    }

    public double DeltaMult { get; set; } = 1d;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _verticalScrollBar = e.NameScope.Find<MyScrollBar>("PART_VerticalScrollBar");
        _horizontalScrollBar = e.NameScope.Find<MyScrollBar>("PART_HorizontalScrollBar");
        _Overscroll.Attach(
            e.NameScope.Find<ScrollContentPresenter>("PART_ScrollContentPresenter"),
            _verticalScrollBar);
    }

    public Thickness ScrollBarMargin
    {
        get => GetValue(ScrollBarMarginProperty);
        set => SetValue(ScrollBarMarginProperty, value);
    }

    private double MaximumOffset => Math.Max(0d, Extent.Height - Viewport.Height);

    /// <summary>
    ///     焦点控件通过 <see cref="ScrollViewer.BringIntoViewOnFocusChange" /> 附加属性关闭了自动滚动时
    ///     （例如 <see cref="MySlider" />），不把该控件滚入视野；其余控件保持滚入视野的默认行为。
    /// </summary>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        if (e.Source is Control source && !GetBringIntoViewOnFocusChange(source))
            return;

        if (_isRestoringFocus)
            return;

        base.OnGotFocus(e);
    }

    /// <summary>按像素增量滚动垂直偏移；动画关闭或控件未加载时直接落到目标位置。</summary>
    public void PerformVerticalOffsetDelta(double delta)
    {
        var target = delta * DeltaMult;

        if (!ContainerAnimation.CanAnimate(this))
        {
            _realOffset = Math.Clamp(_realOffset + target, 0d, MaximumOffset);
            Offset = new Vector(Offset.X, _realOffset);
            return;
        }

        // 不具名：连续滚轮事件各自独立叠加；越界由 Clamp 挡在 [0, 可滚动高度]
        var generation = _wheelAnimationGeneration;
        AnimationService.Run(
            new DoubleAnimation(
                animDelta =>
                {
                    if (generation != _wheelAnimationGeneration) return;
                    _realOffset = Math.Clamp(_realOffset + animDelta, 0d, MaximumOffset);
                    Offset = new Vector(Offset.X, _realOffset);
                },
                target,
                WheelAnimationTime,
                0,
                new FluentEaseOut((EasePower)6)));
    }

    private void OnPreviewPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0d || Extent.Height - Viewport.Height <= 0d)
            return;

        // 下拉框展开、多行文本框、复选框中不抢滚轮
        if (e.Source is ComboBox { IsDropDownOpen: true }
            or ComboBoxItem
            or CheckBox
            or TextBox { AcceptsReturn: true })
            return;

        // 内层 ScrollViewer 优先
        if (IsInnerScrollSource(e.Source))
            return;

        // macOS 触控板跟手滚动，保留系统惯性
        var gestures = _ScrollGestures;
        if (gestures.TryGetCurrent(out var native) && native.HasPreciseDeltas)
        {
            e.Handled = true;
            _HandlePreciseScroll(-e.Delta.Y * gestures.PreciseDeltaScale * DeltaMult, native, e.Timestamp);
            Tooltip.Dismiss();
            return;
        }

        if (_realOffset + -e.Delta.Y * WheelScrollStep * DeltaMult is var target
            && Math.Clamp(target, 0d, MaximumOffset) != _realOffset)
            _verticalScrollBar?.NotifyScrolled();
        e.Handled = true;
        PerformVerticalOffsetDelta(-e.Delta.Y * WheelScrollStep);

        Tooltip.Dismiss();
    }

    private void _HandlePreciseScroll(double delta, ScrollGestureInfo native, ulong timestamp)
    {
        var interval = Math.Clamp(timestamp - _lastPreciseTimestamp, 4d, 50d);
        _lastPreciseTimestamp = timestamp;
        _activePreciseViewer = new WeakReference<MyScrollViewer>(this);
        _Overscroll.SetViewport(Viewport.Height);
        _GestureSafetyTimer.Stop();
        _GestureSafetyTimer.Start();

        if (!ContainerAnimation.CanAnimate(this)
            || !_EnsureScrollPhaseMonitor()
            || (native.Phase == ScrollGesturePhase.None && !native.IsMomentum))
        {
            _wheelAnimationGeneration++;
            _Overscroll.Reset();
            _SetVerticalOffset(Math.Clamp(_realOffset + delta, 0d, MaximumOffset));
            return;
        }

        if (native.IsMomentum)
        {
            // 手指如果抬起时仍在拉伸则先回弹；回弹期间丢弃剩余惯性，避免内容被继续推出边界
            if (_Overscroll.IsStretched) _momentumBounced = true;
            _Overscroll.Release();
            if (_momentumBounced || _Overscroll.IsSpringRunning) return;
        }
        else
        {
            _momentumBounced = false;
            _Overscroll.Grab();
        }

        _wheelAnimationGeneration++;
        if (_Overscroll.IsStretched)
        {
            delta = _Overscroll.ContinueStretch(delta);
            if (delta == 0d) return;
        }

        var target = _realOffset + delta;
        var clamped = Math.Clamp(target, 0d, MaximumOffset);
        _SetVerticalOffset(clamped);
        if (target == clamped) return;

        if (native.IsMomentum)
        {
            _momentumBounced = true;
            _Overscroll.Bounce(delta / interval * 1000d);
        }
        else
        {
            _Overscroll.StartStretch(target - clamped);
        }
    }

    // 这边是因为手指抬起是零增量事件，Avalonia 不会派发，只能从系统监视器得知
    private static bool _EnsureScrollPhaseMonitor()
    {
        if (_scrollPhaseMonitorReady is { } ready) return ready;

        var gestures = _ScrollGestures;
        ready = gestures.EnsurePhaseTracking();
        if (ready)
            gestures.PhaseChanged += info =>
            {
                if (_activePreciseViewer?.TryGetTarget(out var viewer) == true)
                    viewer._OnScrollPhaseChanged(info);
            };
        _scrollPhaseMonitorReady = ready;
        return ready;
    }

    private static IScrollGestureService _ScrollGestures =>
        ApplicationRuntime.GetService<IScrollGestureService>() ?? UnsupportedScrollGestureService.Instance;

    private void _OnScrollPhaseChanged(ScrollGestureInfo info)
    {
        if ((info.Phase & (ScrollGesturePhase.Ended | ScrollGesturePhase.Cancelled)) == 0
            && (info.MomentumPhase & ScrollGesturePhase.Ended) == 0)
            return;

        if (_Overscroll.IsStretched) _momentumBounced = true;
        _Overscroll.Release();
    }

    private void _RebaseOverscroll()
    {
        var shown = _Overscroll.Shown;
        if (shown < 0d || _realOffset <= 0d || _realOffset >= MaximumOffset) return;
        if (shown == 0d && !_Overscroll.IsSpringRunning && !_momentumBounced) return;

        _momentumBounced = false;
        _Overscroll.Reset();
        if (shown > 0d) _SetVerticalOffset(Math.Min(_realOffset + shown, MaximumOffset));
    }

    private void _SetVerticalOffset(double offset)
    {
        _realOffset = offset;
        Offset = new Vector(Offset.X, offset);
    }

    private bool IsInnerScrollSource(object? source)
    {
        if (source is not Visual visual)
            return false;

        foreach (var ancestor in visual.GetVisualAncestors())
        {
            if (ReferenceEquals(ancestor, this))
                return false;
            if (ancestor is ScrollViewer)
                return true;
        }

        return false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as WindowBase;
        if (_window is not null)
            _window.Activated += OnWindowActivated;
        // 提前安装，否则第一次手势的按下事件会漏掉
        _EnsureScrollPhaseMonitor();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_window is not null)
            _window.Activated -= OnWindowActivated;
        _window = null;
        _GestureSafetyTimer.Stop();
        _Overscroll.Reset();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        _isRestoringFocus = true;
        Dispatcher.UIThread.Post(() => _isRestoringFocus = false);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        _realOffset = Offset.Y;
        if (e.ExtentDelta.Y != 0d || e.ViewportDelta.Y != 0d)
            _RebaseOverscroll();
        if (e.OffsetDelta.Y != 0d || e.OffsetDelta.X != 0d)
        {
            if (e.OffsetDelta.Y != 0d) _verticalScrollBar?.NotifyScrolled();
            if (e.OffsetDelta.X != 0d) _horizontalScrollBar?.NotifyScrolled();
            NotifyBackButtonRefresh();
        }
    }

    private static void NotifyBackButtonRefresh()
    {
        ExtraButtonsPanel.Current?.RefreshBackButton();
    }
}