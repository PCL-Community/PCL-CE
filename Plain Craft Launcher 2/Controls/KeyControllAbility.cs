using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

public static class KeyControllAbility
{
    public static readonly DependencyProperty CanSelectProperty =
        DependencyProperty.RegisterAttached("CanSelect", typeof(bool), typeof(KeyControllAbility),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanActivateProperty =
        DependencyProperty.RegisterAttached("CanActivate", typeof(bool), typeof(KeyControllAbility),
            new PropertyMetadata(false));

    public static bool GetCanSelect(DependencyObject obj) => (bool)obj.GetValue(CanSelectProperty);
    public static void SetCanSelect(DependencyObject obj, bool value) => obj.SetValue(CanSelectProperty, value);

    public static bool GetCanActivate(DependencyObject obj) => (bool)obj.GetValue(CanActivateProperty);
    public static void SetCanActivate(DependencyObject obj, bool value) => obj.SetValue(CanActivateProperty, value);

    public static FrameworkElement FindFirstSelectable(DependencyObject? parent)
    {
        if (parent is null) return null;

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe
                && fe.IsVisible
                && fe.IsEnabled
                && GetCanSelect(fe))
                return fe;

            var found = FindFirstSelectable(child);
            if (found is not null)
                return found;
        }
        return null;
    }

    public static void Activate(UIElement target)
    {
        var clickTarget = (target as FrameworkElement)?.FindName("PanClick") as UIElement ?? target;

        if (!clickTarget.IsEnabled || !clickTarget.IsVisible)
            return;

        clickTarget.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent
        });
        clickTarget.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonUpEvent
        });
    }
}
