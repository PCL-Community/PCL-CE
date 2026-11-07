using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace PCL.UI.Controls.Behaviors;

/// <summary>
///     元素进入最近的 <see cref="ScrollViewer" /> 可视区域后执行一次动作；不在滚动容器内时不触发。
/// </summary>
public static class LazyLoader
{
    public static void EnableLazyLoad(this Control element, Action action)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(action);

        element.LayoutUpdated += OnLayoutUpdated;
        return;

        void OnLayoutUpdated(object? sender, EventArgs e)
        {
            if (!element.IsVisible || element.Bounds.Width <= 0d || element.Bounds.Height <= 0d)
                return;

            var scrollViewer = element.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            if (scrollViewer is null)
                return;

            var point = element.TranslatePoint(new Point(0d, 0d), scrollViewer);
            if (point is null)
                return;

            var elementBounds = new Rect(point.Value, element.Bounds.Size);
            var viewport = new Rect(
                0d,
                0d,
                scrollViewer.Viewport.Width,
                scrollViewer.Viewport.Height);
            if (!viewport.Intersects(elementBounds))
                return;

            element.LayoutUpdated -= OnLayoutUpdated;
            action();
        }
    }
}