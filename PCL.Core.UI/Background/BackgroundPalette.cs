using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI.Theme;
using PCL.Core.UI.Theming;

namespace PCL.Core.UI.Background;

/// <summary>背景颜色和主题画刷的纯计算逻辑。</summary>
public static class BackgroundPalette
{
    public static void RefreshColor(Panel formPan)
    {
        try
        {
            formPan.Background = Config.Preference.Background.BackgroundColorful
                ? CreateColorfulBrush()
                : FindThemeBrush("ColorBrushBackground") ?? Brushes.Transparent;
        }
        catch (Exception ex)
        {
            formPan.Background = Brushes.Transparent;
            LogWrapper.Warn("UI", $"刷新背景颜色失败：{ex.Message}");
        }
    }

    private static LinearGradientBrush CreateColorfulBrush()
    {
        var tone = ThemeService.CurrentTone;
        var (hue, _, _, _) = ThemeService.GetCurrentThemeArgs();
        var darkLight = ThemeService.IsDarkMode ? 0.2d : 1d;
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.9d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.1d, 1d, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(
            new GradientStop(
                OklabColor
                    .FromLch(0.84d * darkLight, tone.C5, hue - 15d)
                    .ToAvaloniaColor(),
                -0.1d));
        brush.GradientStops.Add(
            new GradientStop(
                OklabColor
                    .FromLch(0.96d * darkLight, tone.C7, hue)
                    .ToAvaloniaColor(),
                0.4d));
        brush.GradientStops.Add(
            new GradientStop(
                OklabColor
                    .FromLch(0.84d * darkLight, tone.C5, hue + 15d)
                    .ToAvaloniaColor(),
                1.1d));
        return brush;
    }

    private static IBrush? FindThemeBrush(string key)
    {
        return Application.Current is { } app
            && app.TryFindResource(key, out var resource)
                ? resource as IBrush
                : null;
    }
}