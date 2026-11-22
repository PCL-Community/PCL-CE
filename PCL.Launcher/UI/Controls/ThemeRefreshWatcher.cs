using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL.UI.Controls;

internal static class ThemeRefreshWatcher
{
    public static void Watch(Control control, Action refresh)
    {
        var isWatching = false;

        void OnThemeChanged(ColorTheme _)
        {
            refresh();
        }

        void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (isWatching) return;
            ThemeService.ColorThemeChanged += OnThemeChanged;
            isWatching = true;
        }

        void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (!isWatching) return;
            ThemeService.ColorThemeChanged -= OnThemeChanged;
            isWatching = false;
        }

        control.AttachedToVisualTree += OnAttachedToVisualTree;
        control.DetachedFromVisualTree += OnDetachedFromVisualTree;
    }
}
