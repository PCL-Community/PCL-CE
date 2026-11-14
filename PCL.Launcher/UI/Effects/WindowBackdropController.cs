using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.Logging;

namespace PCL.UI.Effects;

/// <summary>
///     按平台可用性请求窗口透明材质：优先系统模糊材质，其次纯透明，最后不透明。
///     窗口本身保持透明，未获支持时由 <c>TransparencyBackgroundFallback</c> 使用主题背景色兜底。
/// </summary>
public static class WindowBackdropController
{
    private static readonly WindowTransparencyLevel[] FallbackChain =
    [
        WindowTransparencyLevel.Mica,
        WindowTransparencyLevel.AcrylicBlur,
        WindowTransparencyLevel.Blur,
        WindowTransparencyLevel.Transparent,
        WindowTransparencyLevel.None
    ];

    private static bool _levelApplied;

    public static WindowTransparencyLevel CurrentLevel { get; private set; }

    public static void Apply(Window window)
    {
        window.TransparencyLevelHint = FallbackChain;
        window.Background = Brushes.Transparent;

        window.Opened += (_, _) => _ApplyLevel(window);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == TopLevel.ActualTransparencyLevelProperty)
                _ApplyLevel(window);
        };
    }

    private static void _ApplyLevel(Window window)
    {
        var level = window.ActualTransparencyLevel;
        if (_levelApplied && level == CurrentLevel) return;

        _levelApplied = true;
        CurrentLevel = level;
        LogWrapper.Info("UI", $"窗口背景效果: {level}");
    }
}