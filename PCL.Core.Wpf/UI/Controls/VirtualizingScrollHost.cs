using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PCL.Core.UI.Controls;

/// <summary>
///     把内部虚拟化面板的 <see cref="IScrollInfo" /> 转发给外层 <see cref="ScrollViewer" /> 的桥接容器。
///     <para>
///         WPF 只会把滚动信息交给 ScrollViewer 的「直接内容」，而 ItemsControl 的直接内容固定是 ItemsPresenter，
///         因此仅将虚拟化面板放进 ItemsPanel 时面板永远拿不到 ScrollOwner，也就无法知道可视区尺寸（不会虚拟化）。
///     </para>
///     <para>
///         用法一（列表自持滚动）：把 ItemsControl 包在本容器中，且本容器作为 ScrollViewer 的直接内容。
///         ScrollViewer 会把本容器当作 IScrollInfo，本容器再把 ScrollOwner 与滚动通知转发给内部的虚拟化面板。
///     </para>
///     <para>
///         用法二（跟随整页滚动）：把本容器放进页面的 StackPanel 中。此时本容器拿不到 ScrollOwner，
///         但仍能找到祖先 ScrollViewer，并把「当前可视带」告诉内部面板：面板只实例化可视区内的行、
///         按绝对坐标排列，完整高度照常返回，页面滚动保持原样。
///     </para>
/// </summary>
public class VirtualizingScrollHost : Decorator, IScrollInfo
{
    private ScrollViewer? _scrollOwner;
    private ScrollViewer? _pageScrollViewer;
    private IScrollInfo? _inner;
    private Size _extent;
    private Size _viewport;
    private double _offsetY;
    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll = true;

    public VirtualizingScrollHost()
    {
        Loaded += (_, _) => AttachPageScrollViewer();
        Unloaded += (_, _) => DetachPageScrollViewer();
        SizeChanged += (_, _) => UpdatePageBand();
    }

    /// <summary>内部虚拟化面板（延迟查找：ItemsPresenter 要在首次测量时才创建面板）。</summary>
    private IScrollInfo? Inner => _inner ??= FindScrollInfo();

    /// <summary>是否处于「跟随整页滚动」模式：自己不是 ScrollViewer 的直接内容，但存在祖先 ScrollViewer。</summary>
    private bool IsPageScrollMode => _scrollOwner is null && _pageScrollViewer is not null;

    #region 布局

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_pageScrollViewer is null)
            AttachPageScrollViewer();
        if (IsPageScrollMode)
        {
            var pageConstraint = new Size(availableSize.Width, double.PositiveInfinity);
            UpdatePageBand();
            Child?.Measure(pageConstraint);

            UpdatePageBand();
            Child?.Measure(pageConstraint);
            return Child?.DesiredSize ?? default;
        }

        var constraint = availableSize;

        if (_scrollOwner is not null && _scrollOwner.ViewportHeight > 0d && double.IsInfinity(availableSize.Height))
            constraint = new Size(availableSize.Width, _scrollOwner.ViewportHeight);

        Child?.Measure(constraint);
        SyncInner();

        var inner = Inner;
        if (inner is not null)
        {
            _extent = new Size(inner.ExtentWidth, inner.ExtentHeight);
            _viewport = new Size(inner.ViewportWidth, inner.ViewportHeight);
        }

        return Child?.DesiredSize ?? default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void SyncInner()
    {
        var inner = Inner;
        if (inner is null)
            return;

        if (!ReferenceEquals(inner.ScrollOwner, _scrollOwner))
            inner.ScrollOwner = _scrollOwner;
        inner.CanVerticallyScroll = _canVerticallyScroll;
        inner.CanHorizontallyScroll = _canHorizontallyScroll;
    }

    #region 跟随整页滚动

    private void AttachPageScrollViewer()
    {
        var viewer = FindAncestorScrollViewer();
        if (ReferenceEquals(viewer, _pageScrollViewer))
            return;
        DetachPageScrollViewer();
        _pageScrollViewer = viewer;
        if (viewer is null)
            return;
        viewer.ScrollChanged += PageScrollViewer_ScrollChanged;
        viewer.SizeChanged += PageScrollViewer_SizeChanged;
        UpdatePageBand();
    }

    private void DetachPageScrollViewer()
    {
        if (_pageScrollViewer is null)
            return;
        _pageScrollViewer.ScrollChanged -= PageScrollViewer_ScrollChanged;
        _pageScrollViewer.SizeChanged -= PageScrollViewer_SizeChanged;
        _pageScrollViewer = null;
    }

    private void PageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdatePageBand();

    private void PageScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePageBand();

    /// <summary>找到最近的祖先 ScrollViewer（页面级滚动通常由它负责）。</summary>
    private ScrollViewer? FindAncestorScrollViewer()
    {
        for (DependencyObject? current = VisualTreeHelper.GetParent(this);
             current is not null;
             current = VisualTreeHelper.GetParent(current))
            if (current is ScrollViewer viewer)
                return viewer;
        return null;
    }

    /// <summary>把祖先 ScrollViewer 的可视带换算到本容器的坐标系，并交给内部虚拟化面板。</summary>
    private void UpdatePageBand()
    {
        if (!IsPageScrollMode || Inner is not IVirtualizingScrollBand band)
            return;

        var viewer = _pageScrollViewer!;
        var viewportHeight = viewer.ViewportHeight > 0d
            ? viewer.ViewportHeight
            : ActualHeight > 0d ? ActualHeight : 600d;

        double topInViewport;
        try
        {
            topInViewport = TransformToAncestor(viewer).Transform(new Point(0d, 0d)).Y;
        }
        catch (InvalidOperationException)
        {
            band.SetExternalViewport(0d, viewportHeight);
            return;
        }
        catch (ArgumentException)
        {
            band.SetExternalViewport(0d, viewportHeight);
            return;
        }

        var topInContent = viewer.VerticalOffset + topInViewport;
        band.SetExternalViewport(viewer.VerticalOffset - topInContent, viewportHeight);
    }

    #endregion

    private IScrollInfo? FindScrollInfo()
    {
        if (Child is null)
            return null;
        if (Child is IScrollInfo direct)
            return direct;

        var queue = new Queue<DependencyObject>();
        queue.Enqueue(Child);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < count; i++)
            {
                var next = VisualTreeHelper.GetChild(current, i);
                if (next is IScrollInfo info)
                    return info;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    #endregion

    #region IScrollInfo

    public ScrollViewer? ScrollOwner
    {
        get => _scrollOwner;
        set
        {
            if (ReferenceEquals(_scrollOwner, value))
                return;
            _scrollOwner = value;

            if (value is not null && Inner is IVirtualizingScrollBand band)
                band.ClearExternalViewport();

            SyncInner();
            InvalidateMeasure();
        }
    }

    public bool CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set
        {
            _canHorizontallyScroll = value;
            SyncInner();
        }
    }

    public bool CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set
        {
            _canVerticallyScroll = value;
            SyncInner();
        }
    }

    public double ExtentWidth => Inner?.ExtentWidth ?? _extent.Width;

    public double ExtentHeight => Inner?.ExtentHeight ?? _extent.Height;

    public double ViewportWidth => Inner?.ViewportWidth ?? _viewport.Width;

    public double ViewportHeight => Inner?.ViewportHeight ?? _viewport.Height;

    public double HorizontalOffset => Inner?.HorizontalOffset ?? 0d;

    public double VerticalOffset => Inner?.VerticalOffset ?? _offsetY;

    public void LineUp() => ScrollBy(-16d);

    public void LineDown() => ScrollBy(16d);

    public void LineLeft() { }

    public void LineRight() { }

    public void PageUp() => ScrollBy(-(ViewportHeight > 0d ? ViewportHeight : 0d));

    public void PageDown() => ScrollBy(ViewportHeight > 0d ? ViewportHeight : 0d);

    public void PageLeft() { }

    public void PageRight() { }

    public void MouseWheelUp() => ScrollBy(-48d);

    public void MouseWheelDown() => ScrollBy(48d);

    public void MouseWheelLeft() { }

    public void MouseWheelRight() { }

    public void SetHorizontalOffset(double offset) { }

    public void SetVerticalOffset(double offset)
    {
        var inner = Inner;
        if (inner is not null)
        {
            inner.SetVerticalOffset(offset);
            return;
        }

        if (Math.Abs(_offsetY - offset) < 0.01d)
            return;
        _offsetY = offset;
        _scrollOwner?.InvalidateScrollInfo();
        InvalidateArrange();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        var inner = Inner;
        return inner?.MakeVisible(visual, rectangle) ?? rectangle;
    }

    private void ScrollBy(double delta)
    {
        var inner = Inner;
        if (inner is not null)
        {
            var target = Math.Max(0d, Math.Min(inner.ExtentHeight - inner.ViewportHeight, inner.VerticalOffset + delta));
            inner.SetVerticalOffset(target);
            return;
        }

        SetVerticalOffset(_offsetY + delta);
    }

    #endregion
}
