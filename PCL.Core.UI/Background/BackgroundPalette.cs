using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI.Theme;

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
        var (hue, _, _) = ThemeService.GetCurrentThemeArgs();
        var darkLight = ThemeService.IsDarkMode ? 0.2d : 1d;
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.9d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.1d, 1d, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(
            new GradientStop(
                ToColor(LabColor.FromLch(0.84d * darkLight, tone.C5, hue - 15d)),
                -0.1d));
        brush.GradientStops.Add(
            new GradientStop(
                ToColor(LabColor.FromLch(0.96d * darkLight, tone.C7, hue)),
                0.4d));
        brush.GradientStops.Add(
            new GradientStop(
                ToColor(LabColor.FromLch(0.84d * darkLight, tone.C5, hue + 15d)),
                1.1d));
        return brush;
    }

    private static Color ToColor(LabColor lab)
    {
        var (alpha, red, green, blue) = lab.ToScRgb();
        return Color.FromArgb(
            SrgbByte(alpha),
            SrgbByte(red),
            SrgbByte(green),
            SrgbByte(blue));
    }

    private static byte SrgbByte(double linear)
    {
        var encoded = linear <= 0.0031308d
            ? 12.92d * linear
            : 1.055d * Math.Pow(linear, 1d / 2.4d) - 0.055d;
        return (byte)Math.Clamp(Math.Round(encoded * 255d), 0d, 255d);
    }

    private static IBrush? FindThemeBrush(string key)
    {
        return Application.Current is { } app
            && app.TryFindResource(key, out var resource)
                ? resource as IBrush
                : null;
    }
}