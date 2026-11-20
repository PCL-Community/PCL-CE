using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Views.Shell;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 滚动容器。滚轮按 300 ms 减速动画改变垂直偏移，<see cref="DeltaMult" /> 为倍率；
///     模板见 <c>UI/Styles/Controls/ScrollBars.axaml</c>，垂直与水平滚动条均为 <see cref="MyScrollBar" />。
/// </summary>
public class MyScrollViewer : ScrollViewer
{
    private const double WheelScrollStep = 120d; // 一个滚轮刻度对应的像素数
    private const int WheelAnimationTime = 300;

    public static readonly StyledProperty<Thickness> ScrollBarMarginProperty =
        AvaloniaProperty.Register<MyScrollViewer, Thickness>(nameof(ScrollBarMargin));

    private double _realOffset;
    private WindowBase? _window;
    private bool _isRestoringFocus;
    private MyScrollBar? _verticalScrollBar;
    private MyScrollBar? _horizontalScrollBar;

    public MyScrollViewer()
    {
        ContainerTheme.AttachStyles(this);

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
        AnimationService.Run(
            new DoubleAnimation(
                animDelta =>
                {
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

        if (_realOffset + -e.Delta.Y * WheelScrollStep * DeltaMult is var target
            && Math.Clamp(target, 0d, MaximumOffset) != _realOffset)
            _verticalScrollBar?.NotifyScrolled();
        e.Handled = true;
        PerformVerticalOffsetDelta(-e.Delta.Y * WheelScrollStep);

        Tooltip.Dismiss();
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
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_window is not null)
            _window.Activated -= OnWindowActivated;
        _window = null;
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