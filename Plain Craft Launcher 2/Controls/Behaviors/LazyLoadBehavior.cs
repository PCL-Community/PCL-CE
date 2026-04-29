using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace PCL;

internal static class LazyLoader
{
    public static void EnableLazyLoad(this FrameworkElement element, Action action)
    {
        var behavior = new LazyLoadBehavior();
        behavior.Action = action;
        Interaction.GetBehaviors(element).Add(behavior);
    }
}

public class LazyLoadBehavior : Behavior<FrameworkElement>
{
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action),
        typeof(Action), typeof(LazyLoadBehavior), new PropertyMetadata(null));

    public Action Action
    {
        get => (Action)GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.LayoutUpdated -= OnLayoutUpdated;
        base.OnDetaching();
    }

    private void OnLayoutUpdated(object sender, EventArgs e)
    {
        if (AssociatedObject.RenderSize.Width < double.Epsilon)
            return;
        if (!AssociatedObject.IsVisible)
            return;

        var scrollViewer = FindParentScrollViewer(AssociatedObject);
        if (scrollViewer is null)
            return;

        var elementBounds = AssociatedObject.TransformToAncestor(scrollViewer)
            .TransformBounds(new Rect(new Point(0d, 0d), AssociatedObject.RenderSize));
        var viewport = new Rect(0d, 0d, scrollViewer.ViewportWidth, scrollViewer.ViewportHeight);

        if (viewport.IntersectsWith(elementBounds))
        {
            Action?.Invoke();
            // 仅执行一次
            AssociatedObject.LayoutUpdated -= OnLayoutUpdated;
        }
    }

    private ScrollViewer FindParentScrollViewer(DependencyObject d)
    {
        while (d is not null)
        {
            if (d is ScrollViewer)
                return (ScrollViewer)d;
            d = VisualTreeHelper.GetParent(d);
        }

        return null;
    }
}