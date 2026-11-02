using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using static PCL.Animations;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 滚动容器。滚轮按 300 ms 减速动画改变垂直偏移，<see cref="DeltaMult" /> 为倍率；
///     模板见 <c>UI/Styles/Controls/ScrollBars.axaml</c>，垂直与水平滚动条均为 <see cref="MyScrollBar" />。
/// </summary>
public class MyScrollViewer : ScrollViewer
{
    private const double WheelScrollStep = 120d; // 一个滚轮刻度对应的像素数
    private const int WheelAnimationTime = 300;
    private const string VerticalBarPartName = "PART_VerticalScrollBar";
    private const string HorizontalBarPartName = "PART_HorizontalScrollBar";

    private double _realOffset;

    /// <summary>模板中的垂直滚动条；模板应用后可用。</summary>
    public MyScrollBar? scrollBar;

    /// <summary>模板中的水平滚动条；模板应用后可用。</summary>
    public MyScrollBar? horizontalScrollBar;

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

    /// <summary>
    ///     焦点控件通过 <see cref="ScrollViewer.BringIntoViewOnFocusChange" /> 附加属性关闭了自动滚动时
    ///     （例如 <see cref="MySlider" />），不把该控件滚入视野；其余控件保持滚入视野的默认行为。
    /// </summary>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        if (e.Source is Control source && !GetBringIntoViewOnFocusChange(source))
            return;

        base.OnGotFocus(e);
    }

    /// <summary>滚轮滚动倍率。</summary>
    public double DeltaMult { get; set; } = 1d;

    private double MaximumOffset => Math.Max(0d, Extent.Height - Viewport.Height);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        scrollBar = e.NameScope.Find<MyScrollBar>(VerticalBarPartName);
        horizontalScrollBar = e.NameScope.Find<MyScrollBar>(HorizontalBarPartName);
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
        AniStart(
            AaDouble(
                animDelta =>
                {
                    _realOffset = Math.Clamp(_realOffset + animDelta, 0d, MaximumOffset);
                    Offset = new Vector(Offset.X, _realOffset);
                },
                target,
                WheelAnimationTime,
                0,
                new AniEaseOutFluent((AniEasePower)6)));
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

        e.Handled = true;
        PerformVerticalOffsetDelta(-e.Delta.Y * WheelScrollStep);

        // TODO(FIX-F-01): 滚轮滚动时关闭已弹出的提示；界面层尚无全局 Tooltip 服务。
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

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        _realOffset = Offset.Y;
        if (e.OffsetDelta.Y != 0d || e.ViewportDelta.Y != 0d)
            NotifyBackButtonRefresh();
    }

    private void NotifyBackButtonRefresh()
    {
        // TODO(FIX-F-02): 通知主窗口“返回顶部”按钮刷新显隐；该按钮尚未迁移。
    }
}