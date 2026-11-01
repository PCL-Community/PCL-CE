using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.Logging;

namespace PCL.Avalonia.UI.Effects;

/// <summary>
///     按 Mica、Acrylic、Blur、透明、不透明的顺序请求窗口背景效果，由系统选择可用的最高档。
/// </summary>
public static class WindowBackdropController
{
    private static readonly WindowTransparencyLevel[] _FallbackChain =
    [
        WindowTransparencyLevel.Mica,
        WindowTransparencyLevel.AcrylicBlur,
        WindowTransparencyLevel.Blur,
        WindowTransparencyLevel.Transparent,
        WindowTransparencyLevel.None
    ];

    public static WindowTransparencyLevel CurrentLevel { get; private set; }

    public static void Apply(Window window)
    {
        window.TransparencyLevelHint = _FallbackChain;
        window.TransparencyBackgroundFallback = new SolidColorBrush(Color.Parse("#FF1F1F1F"));
        window.Background = Brushes.Transparent;

        window.Opened += (_, _) => _LogLevel(window);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == TopLevel.ActualTransparencyLevelProperty) _LogLevel(window);
        };
    }

    private static void _LogLevel(Window window)
    {
        CurrentLevel = window.ActualTransparencyLevel;
        LogWrapper.Info("UI", $"窗口背景效果: {CurrentLevel}");
    }
}