using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PCL.Core.UI.Controls;

/// <summary>分段控件中的一个内容项。</summary>
public sealed class SegmentedItem : Border
{
    public SegmentedItem()
    {
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Hand);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }
}