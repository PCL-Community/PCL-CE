using System;
using System.Windows;
using System.Windows.Controls;

namespace PCL;

/// <summary>
/// Owns WPF-only margin, layout, and control freeze helpers.
/// </summary>
public static class LauncherLayout
{
    public static void DeltaLeft(FrameworkElement control, double newValue)
    {
        LauncherLogger.DebugAssert(!double.IsNaN(newValue));
        LauncherLogger.DebugAssert(!double.IsInfinity(newValue));

        if (control is Window window)
        {
            window.Left += newValue;
            return;
        }

        switch (control.HorizontalAlignment)
        {
            case HorizontalAlignment.Left:
            case HorizontalAlignment.Stretch:
                control.Margin = new Thickness(control.Margin.Left + newValue, control.Margin.Top,
                    control.Margin.Right, control.Margin.Bottom);
                return;
            case HorizontalAlignment.Right:
                control.Margin = new Thickness(control.Margin.Left, control.Margin.Top,
                    control.Margin.Right - newValue, control.Margin.Bottom);
                return;
            default:
                LauncherLogger.DebugAssert(false);
                return;
        }
    }

    public static void SetLeft(FrameworkElement control, double newValue)
    {
        LauncherLogger.DebugAssert(control.HorizontalAlignment == HorizontalAlignment.Left);
        control.Margin = new Thickness(newValue, control.Margin.Top, control.Margin.Right, control.Margin.Bottom);
    }

    public static void DeltaTop(FrameworkElement control, double newValue)
    {
        LauncherLogger.DebugAssert(!double.IsNaN(newValue));
        LauncherLogger.DebugAssert(!double.IsInfinity(newValue));

        if (control is Window window)
        {
            window.Top += newValue;
            return;
        }

        switch (control.VerticalAlignment)
        {
            case VerticalAlignment.Top:
                control.Margin = new Thickness(control.Margin.Left, control.Margin.Top + newValue,
                    control.Margin.Right, control.Margin.Bottom);
                return;
            case VerticalAlignment.Bottom:
                control.Margin = new Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right,
                    control.Margin.Bottom - newValue);
                return;
            default:
                LauncherLogger.DebugAssert(false);
                return;
        }
    }

    public static void SetTop(FrameworkElement control, double newValue)
    {
        LauncherLogger.DebugAssert(control.VerticalAlignment == VerticalAlignment.Top);
        control.Margin = new Thickness(control.Margin.Left, newValue, control.Margin.Right, control.Margin.Bottom);
    }

    public static void ControlFreeze(Panel ui)
    {
        ui.Background = LauncherWpf.ControlBrush(ui);
        ui.Children.Clear();
    }

    public static void ControlFreeze(Border ui)
    {
        ui.Background = LauncherWpf.ControlBrush(ui);
        ui.Child = null;
    }
}
