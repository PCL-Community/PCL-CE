using Avalonia;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.UI.Theme;
using PCL.Core.UI.Theming;

namespace PCL.UI.Theming;

/// <summary>按当前自定义主题生成顶部标题栏的三段渐变背景。</summary>
public static class ThemeTitleBarBrushBuilder
{
    public static LinearGradientBrush? Build()
    {
        if (ThemeService.CurrentTheme != ColorTheme.Custom) return null;

        var custom = ThemeService.GetCustomColor(ThemeService.IsDarkMode);
        if (custom.HueSpread == 0) return null;

        var (start, middle, end) = custom.ToTitleBarColors(ThemeService.CurrentTone);
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1d, 0d, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(start.ToAvaloniaColor(), 0d));
        brush.GradientStops.Add(new GradientStop(middle.ToAvaloniaColor(), 0.5d));
        brush.GradientStops.Add(new GradientStop(end.ToAvaloniaColor(), 1d));
        return brush;
    }
}