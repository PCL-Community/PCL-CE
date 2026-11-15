using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PCL.Core.UI.Controls;

/// <summary>
///     由外部滚动容器（如 <see cref="VirtualizingScrollHost" />）告知当前可视区域的面板。
///     <para>用于列表跟随整页滚动的场景：面板自身不滚动，只按外部给出的可视带实例化与排列条目。</para>
/// </summary>
public interface IVirtualizingScrollBand
{
    /// <summary>设置外部可视带。<paramref name="offset" /> 是可视带顶部相对面板内容顶部的位置。</summary>
    void SetExternalViewport(double offset, double viewportHeight);

    /// <summary>取消外部模式，恢复由 ScrollOwner 驱动的自持滚动。</summary>
    void ClearExternalViewport();
}

/// <summary>
///     支持行虚拟化与容器回收的换行面板。
///     <para>与 <see cref="WrapPanel" /> 的布局一致，但只有当前可视区域（以及上下各 <see cref="CacheRows" /> 行缓存）内的条目会被实例化。</para>
///     <para>要求所有条目尺寸一致：宽度由 <see cref="ItemWidth" /> 决定，高度留空时按首个条目自动测量一次。</para>
///     <para>与 <see cref="VirtualizingStackPanel" /> 相同，只有作为 <see cref="ScrollViewer" /> 的直接内容且 CanContentScroll=true 时才会虚拟化。</para>
/// </summary>
public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo, IVirtualizingScrollBand
{
    /// <summary>一行滚动的像素数（与 WPF 默认行高一致）。</summary>
    private const double LineDelta = 16d;

    /// <summary>一个滚轮刻度滚动的像素数（3 行）。</summary>
    private const double WheelDelta = LineDelta * 3d;

    private double _autoItemHeight = double.NaN;
    private int _columns = 1;
    private double _offsetY;
    private int _realizedFirstIndex = -1;
    private int _realizedLastIndex = -1;
    private ScrollViewer? _scrollOwner;
    private Size _extent;
    private Size _viewport;
    private bool _externalMode;
    private double _externalOffset;
    private double _externalViewportHeight;

    #region 依赖属性

    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(200d, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is double and (double.NaN or > 0d));

    /// <summary>每个条目的槽位宽度（含条目自身外边距）。</summary>
    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is double and (double.NaN or > 0d));

    /// <summary>每个条目的槽位高度（含条目自身外边距）。为 NaN 时按首个条目自动测量。</summary>
    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is >= 0d);

    /// <summary>条目之间的水平间距。</summary>
    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
        nameof(VerticalSpacing), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is >= 0d);

    /// <summary>条目之间的垂直间距。</summary>
    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    public static readonly DependencyProperty CacheRowsProperty = DependencyProperty.Register(
        nameof(CacheRows), typeof(int), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is >= 0);

    /// <summary>可视区域上下额外保留的行数，用于减少快速滚动时的反复实例化。</summary>
    public int CacheRows
    {
        get => (int)GetValue(CacheRowsProperty);
        set => SetValue(CacheRowsProperty, value);
    }

    #endregion

    #region 布局

    protected override Size MeasureOverride(Size availableSize)
    {
        var itemCount = GetItemCount();
        if (itemCount == 0)
        {
            CleanUpItems(int.MaxValue, int.MinValue);
            _extent = default;
            _viewport = default;
            _scrollOwner?.InvalidateScrollInfo();
            return default;
        }

        var viewportWidth = GetViewportWidth(availableSize);
        var itemWidth = ResolveItemWidth(viewportWidth);
        // 自动高度需要先实例化一个条目来测量，此时还不知道可视范围
        var itemHeight = GetItemHeight(itemWidth);

        var columnStride = itemWidth + HorizontalSpacing;
        var rowHeight = itemHeight + VerticalSpacing;
        _columns = viewportWidth <= 0d ? 1 : Math.Max(1, (int)Math.Floor((viewportWidth + HorizontalSpacing) / columnStride));
        var rows = (int)Math.Ceiling(itemCount / (double)_columns);

        var extentHeight = rows * rowHeight - (VerticalSpacing > 0d ? VerticalSpacing : 0d);
        _extent = new Size(Math.Min(viewportWidth, _columns * columnStride), Math.Max(0d, extentHeight));

        // 外部滚动模式：可视带由 VirtualizingScrollHost 给出，面板自身不滚动
        var external = _externalMode && _externalViewportHeight > 0d;
        var viewportHeight = external
            ? _externalViewportHeight
            : GetViewportHeight(availableSize, _extent.Height);
        _viewport = new Size(viewportWidth, viewportHeight);
        if (!external)
            CoerceOffset();

        var bandOffset = external ? Math.Max(0d, _externalOffset) : _offsetY;
        int firstRow;
        int lastRow;
        if (external || ShouldVirtualize())
        {
            firstRow = Math.Max(0, (int)Math.Floor(bandOffset / rowHeight) - CacheRows);
            lastRow = Math.Min(rows - 1, (int)Math.Ceiling((bandOffset + viewportHeight) / rowHeight) + CacheRows - 1);
        }
        else
        {
            firstRow = 0;
            lastRow = rows - 1;
        }

        var firstIndex = firstRow * _columns;
        var lastIndex = Math.Min(itemCount - 1, (lastRow + 1) * _columns - 1);
        RealizeItems(firstIndex, lastIndex, new Size(itemWidth, itemHeight));

        _scrollOwner?.InvalidateScrollInfo();

        // 外部模式下必须报告完整高度，整页滚动才能滚到底
        if (external)
            return new Size(_extent.Width, _extent.Height);

        return new Size(_extent.Width,
            double.IsInfinity(availableSize.Height) ? _extent.Height : Math.Min(viewportHeight, _extent.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0)
            return finalSize;

        var viewportWidth = finalSize.Width > 0d ? finalSize.Width : _viewport.Width;
        var itemWidth = ResolveItemWidth(viewportWidth);
        var itemHeight = GetItemHeight(itemWidth);
        var columnStride = itemWidth + HorizontalSpacing;
        var rowHeight = itemHeight + VerticalSpacing;
        var columns = Math.Max(1, _columns);
        // 列整体居中，与原先 WrapPanel 由父控件居中的观感保持一致
        var usedWidth = columns * columnStride - (HorizontalSpacing > 0d ? HorizontalSpacing : 0d);
        var offsetX = Math.Max(0d, (finalSize.Width - usedWidth) / 2d);
        // 外部模式下条目按绝对位置排列，滚动由整页负责
        var baseOffset = _externalMode ? 0d : _offsetY;

        var generator = ItemContainerGenerator;
        for (var childIndex = 0; childIndex < InternalChildren.Count; childIndex++)
        {
            var child = InternalChildren[childIndex];
            var itemIndex = generator?.IndexFromGeneratorPosition(new GeneratorPosition(childIndex, 0)) ?? childIndex;
            if (itemIndex < 0)
                itemIndex = childIndex;
            var row = itemIndex / columns;
            var column = itemIndex % columns;
            child.Arrange(new Rect(offsetX + column * columnStride, row * rowHeight - baseOffset, itemWidth, itemHeight));
        }

        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                if (args.ItemUICount > 0)
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Move:
                if (args.ItemUICount > 0)
                    RemoveInternalChildRange(args.OldPosition.Index, args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Reset:
                RemoveInternalChildRange(0, InternalChildren.Count);
                break;
        }

        _realizedFirstIndex = -1;
        _realizedLastIndex = -1;
        InvalidateMeasure();
    }

    protected override void BringIndexIntoView(int index)
    {
        var itemHeight = GetItemHeight(ResolveItemWidth(_viewport.Width));
        var rowHeight = itemHeight + VerticalSpacing;
        var row = index / Math.Max(1, _columns);
        var rowTop = row * rowHeight;
        var rowBottom = rowTop + rowHeight;
        if (rowTop < _offsetY)
            SetVerticalOffset(rowTop);
        else if (rowBottom > _offsetY + _viewport.Height)
            SetVerticalOffset(rowBottom - _viewport.Height);
    }

    #endregion

    #region 实例化与回收

    private void RealizeItems(int firstIndex, int lastIndex, Size itemSize)
    {
        if (lastIndex < firstIndex)
        {
            CleanUpItems(int.MaxValue, int.MinValue);
            _realizedFirstIndex = -1;
            _realizedLastIndex = -1;
            return;
        }

        var generator = ItemContainerGenerator;
        if (generator is null)
            return;

        var startPosition = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;
        var itemCount = GetItemCount();

        using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
        {
            for (var itemIndex = firstIndex; itemIndex <= lastIndex && itemIndex < itemCount; itemIndex++, childIndex++)
            {
                var child = generator.GenerateNext(out var newlyRealized) as UIElement;
                if (child is null)
                    continue;
                if (newlyRealized)
                {
                    if (childIndex >= InternalChildren.Count)
                        AddInternalChild(child);
                    else
                        InsertInternalChild(childIndex, child);
                    generator.PrepareItemContainer(child);
                }

                child.Measure(itemSize);
            }
        }

        CleanUpItems(firstIndex, lastIndex);
        _realizedFirstIndex = firstIndex;
        _realizedLastIndex = Math.Min(lastIndex, itemCount - 1);
    }

    private void CleanUpItems(int minDesiredIndex, int maxDesiredIndex)
    {
        var generator = ItemContainerGenerator;
        if (generator is null)
            return;
        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var position = new GeneratorPosition(childIndex, 0);
            var itemIndex = generator.IndexFromGeneratorPosition(position);
            if (itemIndex >= minDesiredIndex && itemIndex <= maxDesiredIndex)
                continue;

            if (itemIndex >= 0)
            {
                try
                {
                    generator.Remove(position, 1);
                }
                catch (InvalidOperationException)
                {
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }

            RemoveInternalChildRange(childIndex, 1);
            if (itemIndex >= 0 && itemIndex < _realizedFirstIndex)
                _realizedFirstIndex--;
        }
    }

    /// <summary>实例化单个条目并使用无界高度测量，用于确定自动条目高度。</summary>
    private double MeasureFirstItemHeight(double itemWidth)
    {
        var generator = ItemContainerGenerator;
        if (generator is null)
            return 0d;
        var startPosition = generator.GeneratorPositionFromIndex(0);
        var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;
        double height;
        using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
        {
            var child = generator.GenerateNext(out var newlyRealized) as UIElement;
            if (child is null)
                return 0d;
            if (newlyRealized)
            {
                if (childIndex >= InternalChildren.Count)
                    AddInternalChild(child);
                else
                    InsertInternalChild(childIndex, child);
                generator.PrepareItemContainer(child);
            }

            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            height = child.DesiredSize.Height;
        }

        if (height is double.NaN or <= 0d)
            return 0d;
        _autoItemHeight = ItemHeight is double.NaN ? height : ItemHeight;
        return _autoItemHeight;
    }

    #endregion

    #region 外部可视带

    /// <summary>由外部滚动容器设置可视带：面板自身不滚动，只实例化该带内的条目。</summary>
    public void SetExternalViewport(double offset, double viewportHeight)
    {
        var previousOffset = _externalMode ? _externalOffset : _offsetY;
        var previousHeight = _externalMode ? _externalViewportHeight : _viewport.Height;
        _externalMode = true;
        _externalOffset = offset;
        _externalViewportHeight = Math.Max(0d, viewportHeight);
        if (Math.Abs(previousOffset - _externalOffset) < 0.5d &&
            Math.Abs(previousHeight - _externalViewportHeight) < 0.5d)
            return;

        if (IsCoveredByRealizedRange(_externalOffset))
            InvalidateArrange();
        else
            InvalidateMeasure();
    }

    /// <summary>取消外部模式，恢复由 ScrollOwner 驱动的自持滚动。</summary>
    public void ClearExternalViewport()
    {
        if (!_externalMode)
            return;
        _externalMode = false;
        _externalOffset = 0d;
        _externalViewportHeight = 0d;
        InvalidateMeasure();
    }

    /// <summary>当前是否处于外部可视带模式。</summary>
    public bool IsExternalViewportMode => _externalMode;

    #endregion

    #region IScrollInfo

    public bool CanHorizontallyScroll { get; set; }

    public bool CanVerticallyScroll { get; set; } = true;

    public double ExtentWidth => _extent.Width;

    public double ExtentHeight => _extent.Height;

    public double ViewportWidth => _viewport.Width;

    public double ViewportHeight => _viewport.Height;

    public double HorizontalOffset => 0d;

    public double VerticalOffset => _offsetY;

    public ScrollViewer? ScrollOwner
    {
        get => _scrollOwner;
        set
        {
            if (ReferenceEquals(_scrollOwner, value))
                return;
            _scrollOwner = value;
            InvalidateMeasure();
        }
    }

    public void LineUp() => SetVerticalOffset(_offsetY - LineDelta);

    public void LineDown() => SetVerticalOffset(_offsetY + LineDelta);

    public void LineLeft()
    {
    }

    public void LineRight()
    {
    }

    public void PageUp() => SetVerticalOffset(_offsetY - _viewport.Height);

    public void PageDown() => SetVerticalOffset(_offsetY + _viewport.Height);

    public void PageLeft()
    {
    }

    public void PageRight()
    {
    }

    public void MouseWheelUp() => SetVerticalOffset(_offsetY - WheelDelta);

    public void MouseWheelDown() => SetVerticalOffset(_offsetY + WheelDelta);

    public void MouseWheelLeft()
    {
    }

    public void MouseWheelRight()
    {
    }

    public void SetHorizontalOffset(double offset)
    {
    }

    public void SetVerticalOffset(double offset)
    {
        var maxOffset = Math.Max(0d, _extent.Height - _viewport.Height);
        offset = Math.Clamp(offset, 0d, maxOffset);
        if (Math.Abs(offset - _offsetY) < 0.01d)
            return;
        _offsetY = offset;
        _scrollOwner?.InvalidateScrollInfo();
        if (IsCoveredByRealizedRange(offset))
            InvalidateArrange(); // 已实例化的条目足够覆盖新的可视范围，只需重新排布
        else
            InvalidateMeasure(); // 需要实例化新的行
    }

    public Rect MakeVisible(Visual? visual, Rect rectangle)
    {
        if (rectangle.IsEmpty || visual is null)
            return Rect.Empty;
        for (var childIndex = 0; childIndex < InternalChildren.Count; childIndex++)
        {
            var child = InternalChildren[childIndex];
            if (!IsDescendantOf(visual, child))
                continue;
            var itemIndex = ItemContainerGenerator?.IndexFromGeneratorPosition(new GeneratorPosition(childIndex, 0)) ?? -1;
            if (itemIndex >= 0)
                BringIndexIntoView(itemIndex);
            var bounds = child.TransformToAncestor(this).TransformBounds(new Rect(child.RenderSize));
            if (bounds.Top < _offsetY)
                SetVerticalOffset(bounds.Top);
            else if (bounds.Bottom > _offsetY + _viewport.Height)
                SetVerticalOffset(bounds.Bottom - _viewport.Height);
            return bounds;
        }

        return Rect.Empty;
    }

    private static bool IsDescendantOf(Visual visual, DependencyObject ancestor)
    {
        for (DependencyObject? current = visual; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor))
                return true;
        return false;
    }

    #endregion

    #region 辅助

    private int GetItemCount()
    {
        var owner = ItemsControl.GetItemsOwner(this);
        return owner?.Items.Count ?? 0;
    }

    private double ResolveItemWidth(double viewportWidth)
    {
        var width = ItemWidth;
        if (width is not double.NaN && width > 0d)
            return width;
        // NaN 表示铺满可视宽度（单列列表，如资源列表）
        return viewportWidth > 0d ? viewportWidth : 1d;
    }

    private double GetItemHeight(double itemWidth)
    {
        var configured = ItemHeight;
        if (configured is not double.NaN && configured > 0d)
            return configured;
        if (_autoItemHeight is not double.NaN && _autoItemHeight > 0d)
            return _autoItemHeight;
        return MeasureFirstItemHeight(itemWidth);
    }

    /// <summary>
    ///     是否按可视区域实例化条目。视口高度无界（例如放在 StackPanel 里）时视口等于整体高度，等价于全部实例化。
    /// </summary>
    private bool ShouldVirtualize()
    {
        if (_viewport.Height <= 0d || double.IsInfinity(_viewport.Height))
            return false;
        var owner = ItemsControl.GetItemsOwner(this);
        return owner is null || GetIsVirtualizing(owner);
    }

    /// <summary>判断滚动到新的偏移后，可视范围是否仍被已实例化的条目完全覆盖。</summary>
    private bool IsCoveredByRealizedRange(double offset)
    {
        if (_realizedFirstIndex < 0 || _realizedLastIndex < 0)
            return false;
        if (_columns <= 0 || _viewport.Height <= 0d)
            return false;
        var itemHeight = ItemHeight is double.NaN ? _autoItemHeight : ItemHeight;
        if (itemHeight is double.NaN or <= 0d)
            return false;
        var rowHeight = itemHeight + VerticalSpacing;
        if (rowHeight <= 0d)
            return false;
        var firstRow = Math.Max(0, (int)Math.Floor(offset / rowHeight));
        var lastRow = (int)Math.Ceiling((offset + _viewport.Height) / rowHeight) - 1;
        var firstIndex = firstRow * _columns;
        var lastIndex = (lastRow + 1) * _columns - 1;
        return firstIndex >= _realizedFirstIndex && lastIndex <= _realizedLastIndex;
    }

    private double GetViewportWidth(Size availableSize)
    {
        var width = _scrollOwner?.ViewportWidth ?? 0d;
        if (width <= 0d)
            width = double.IsInfinity(availableSize.Width) ? _extent.Width : availableSize.Width;
        return width <= 0d ? 0d : width;
    }

    private double GetViewportHeight(Size availableSize, double extentHeight)
    {
        var height = _scrollOwner?.ViewportHeight ?? 0d;
        if (height <= 0d)
            height = double.IsInfinity(availableSize.Height) ? extentHeight : availableSize.Height;
        return height <= 0d ? extentHeight : height;
    }

    private void CoerceOffset()
    {
        var maxOffset = Math.Max(0d, _extent.Height - _viewport.Height);
        if (_offsetY > maxOffset)
            _offsetY = maxOffset;
        if (_offsetY < 0d)
            _offsetY = 0d;
    }

    #endregion
}
