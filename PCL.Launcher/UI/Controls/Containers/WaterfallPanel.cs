using Avalonia;
using Avalonia.Controls;

namespace PCL.UI.Controls;

/// <summary>瀑布流布局模式。</summary>
public enum WaterfallLayoutMode
{
    FixedColumns,

    AutoFit
}

/// <summary>
///     瀑布流面板：子元素按当前高度最小的列依次排列，列宽由可用宽度等分。
///     Avalonia 没有内置瀑布流布局，这里自行实现测量与排列；子元素宽度被约束为列宽，高度不限。
/// </summary>
public class WaterfallPanel : Panel
{
    public static readonly StyledProperty<WaterfallLayoutMode> LayoutModeProperty =
        AvaloniaProperty.Register<WaterfallPanel, WaterfallLayoutMode>(
            nameof(LayoutMode));

    public static readonly StyledProperty<int> ColumnCountProperty =
        AvaloniaProperty.Register<WaterfallPanel, int>(nameof(ColumnCount), 2);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(ColumnSpacing), 5d);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(RowSpacing));

    public static readonly StyledProperty<double> MaxItemWidthProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(MaxItemWidth), 100d);

    private int _computedColumnCount;

    static WaterfallPanel()
    {
        AffectsMeasure<WaterfallPanel>(
            LayoutModeProperty,
            ColumnCountProperty,
            ColumnSpacingProperty,
            RowSpacingProperty,
            MaxItemWidthProperty);
    }

    public WaterfallLayoutMode LayoutMode
    {
        get => GetValue(LayoutModeProperty);
        set => SetValue(LayoutModeProperty, value);
    }

    public int ColumnCount
    {
        get => GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, Math.Max(1, value));
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, Math.Max(0d, value));
    }

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, Math.Max(0d, value));
    }

    public double MaxItemWidth
    {
        get => GetValue(MaxItemWidthProperty);
        set => SetValue(MaxItemWidthProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var columnCount = _ResolveColumnCount(availableSize.Width);
        _computedColumnCount = columnCount;

        var columnWidth = _ResolveColumnWidth(availableSize.Width, columnCount);
        var columnHeights = new double[columnCount];

        foreach (var child in Children)
        {
            child.Measure(new Size(columnWidth, double.PositiveInfinity));

            var columnIndex = _GetMinHeightColumnIndex(columnHeights);
            columnHeights[columnIndex] += child.DesiredSize.Height + RowSpacing;
        }

        var totalHeight = columnHeights
            .Select(height => height > 0d ? height - RowSpacing : height)
            .Prepend(0d)
            .Max();

        var width = double.IsInfinity(availableSize.Width) ? 0d : availableSize.Width;
        return new Size(width, totalHeight);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_computedColumnCount <= 0) return finalSize;

        var columnWidth = _ResolveColumnWidth(finalSize.Width, _computedColumnCount);
        var columnHeights = new double[_computedColumnCount];

        foreach (var child in Children)
        {
            var columnIndex = _GetMinHeightColumnIndex(columnHeights);
            var x = columnIndex * (columnWidth + ColumnSpacing);
            var y = columnHeights[columnIndex];

            child.Arrange(new Rect(x, y, columnWidth, child.DesiredSize.Height));
            columnHeights[columnIndex] += child.DesiredSize.Height + RowSpacing;
        }

        return finalSize;
    }

    private int _ResolveColumnCount(double availableWidth)
    {
        if (LayoutMode == WaterfallLayoutMode.FixedColumns
            || double.IsInfinity(availableWidth)
            || double.IsNaN(availableWidth))
            return Math.Max(1, ColumnCount);

        var effectiveMaxWidth = MaxItemWidth > 0d ? MaxItemWidth : 100d;
        var slotWidth = effectiveMaxWidth + ColumnSpacing;
        return Math.Max(1, (int)Math.Floor((availableWidth + ColumnSpacing) / slotWidth));
    }

    private double _ResolveColumnWidth(double availableWidth, int columnCount)
    {
        if (double.IsInfinity(availableWidth) || double.IsNaN(availableWidth))
            return Math.Max(1d, MaxItemWidth > 0d ? MaxItemWidth : 100d);

        var columnWidth = (availableWidth - (columnCount - 1) * ColumnSpacing) / columnCount;
        return Math.Max(0d, columnWidth);
    }

    private static int _GetMinHeightColumnIndex(double[] columnHeights)
    {
        var minIndex = 0;
        var minHeight = columnHeights[0];

        for (var i = 1; i < columnHeights.Length; i++)
            if (columnHeights[i] < minHeight)
            {
                minHeight = columnHeights[i];
                minIndex = i;
            }

        return minIndex;
    }
}