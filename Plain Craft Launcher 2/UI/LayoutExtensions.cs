using System.Windows;

namespace PCL;

/// <summary>
///     历史布局辅助方法。
/// </summary>
public static class LayoutExtensions
{
    public static void DeltaLeft(FrameworkElement control, double newValue)
    {
        LauncherLog.DebugAssert(!double.IsNaN(newValue));
        LauncherLog.DebugAssert(!double.IsInfinity(newValue));

        if (control is Window window)
            window.Left += newValue;
        else
            switch (control.HorizontalAlignment)
            {
                case HorizontalAlignment.Left or HorizontalAlignment.Stretch:
                    control.Margin = new Thickness(
                        control.Margin.Left + newValue,
                        control.Margin.Top,
                        control.Margin.Right,
                        control.Margin.Bottom);
                    break;

                case HorizontalAlignment.Right:
                    control.Margin = new Thickness(
                        control.Margin.Left,
                        control.Margin.Top,
                        control.Margin.Right - newValue,
                        control.Margin.Bottom);
                    break;

                case HorizontalAlignment.Center:
                default:
                    LauncherLog.DebugAssert(false);
                    break;
            }
    }

    public static void SetLeft(FrameworkElement control, double newValue)
    {
        LauncherLog.DebugAssert(control.HorizontalAlignment == HorizontalAlignment.Left);
        control.Margin = new Thickness(
            newValue,
            control.Margin.Top,
            control.Margin.Right,
            control.Margin.Bottom);
    }

    public static void DeltaTop(FrameworkElement control, double newValue)
    {
        LauncherLog.DebugAssert(!double.IsNaN(newValue));
        LauncherLog.DebugAssert(!double.IsInfinity(newValue));

        if (control is Window window)
            window.Top += newValue;
        else
            switch (control.VerticalAlignment)
            {
                case VerticalAlignment.Top:
                    control.Margin = new Thickness(
                        control.Margin.Left,
                        control.Margin.Top + newValue,
                        control.Margin.Right,
                        control.Margin.Bottom);
                    break;

                case VerticalAlignment.Bottom:
                    control.Margin = new Thickness(
                        control.Margin.Left,
                        control.Margin.Top,
                        control.Margin.Right,
                        control.Margin.Bottom - newValue);
                    break;

                case VerticalAlignment.Center or VerticalAlignment.Stretch:
                default:
                    LauncherLog.DebugAssert(false);
                    break;
            }
    }

    public static void SetTop(FrameworkElement control, double newValue)
    {
        LauncherLog.DebugAssert(control.VerticalAlignment == VerticalAlignment.Top);
        control.Margin = new Thickness(
            control.Margin.Left,
            newValue,
            control.Margin.Right,
            control.Margin.Bottom);
    }
}