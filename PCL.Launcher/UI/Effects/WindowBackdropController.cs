using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.Logging;

namespace PCL.UI.Effects;

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

    private static WindowTransparencyLevel CurrentLevel { get; set; }

    public static void Apply(Window window)
    {
        window.TransparencyLevelHint = _FallbackChain;
        // 窗口必须保持透明，系统材质才能从窗口下方透出；
        // 内容可读性由 WindowSurface 壳层与主题色负责，不能用整窗不透明底色遮挡材质。
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