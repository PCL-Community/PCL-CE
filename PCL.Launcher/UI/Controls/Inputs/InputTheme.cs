using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

/// <summary>
///     输入/选择控件共用的主题画刷解析。
///     颜色统一取自 ThemeService 写入的核心 <c>ColorBrush*</c> 动态资源；
///     控件尚未挂进可视树或资源缺失时使用调用方给出的回退画刷。
/// </summary>
internal static class InputTheme
{
    internal const string TextKey = "ColorBrush1";
    internal const string TextSecondaryKey = "ColorBrushGray3";
    internal const string AccentKey = "ColorBrush3";
    internal const string AccentHoverKey = "ColorBrush4";
    internal const string AccentLightKey = "ColorBrush7";
    internal const string ItemHoverKey = "ColorBrushBg1";
    internal const string ItemCheckedKey = "ColorBrush7";
    internal const string CardKey = "ColorBrushWhite";
    internal const string CardBorderKey = "ColorBrushGray6";
    internal const string DividerKey = "ColorBrushGray5";
    internal const string IdleBorderKey = "ColorBrushBg0";
    internal const string IdleBackgroundKey = "ColorBrushHalfWhite";
    internal const string DisabledBorderKey = "ColorBrushGray5";
    internal const string DisabledBackgroundKey = "ColorBrushGray6";
    internal const string ErrorKey = "ColorBrushRedLight";
    internal const string ErrorBackgroundKey = "ColorBrushRedBack";
    internal const string SemiTransparentKey = "ColorBrushSemiTransparent";
    internal const string CheckFillKey = "ColorBrushHalfWhite";
    internal const string CheckPressedKey = "ColorBrushBg1";
    internal const string CheckedKey = "ColorBrush2";
    internal const string HoverKey = "ColorBrush3";
    internal const string DisabledTextKey = "ColorBrushGray4";
    internal const string ComboSelectedKey = "ColorBrush6";
    internal const string ComboHoverKey = "ColorBrush8";

    private static int _uuidSeed;

    /// <summary>
    ///     生成控件编号，用于区分同一控件族内的动画组名与日志。
    /// </summary>
    public static int NewUuid()
    {
        return Interlocked.Increment(ref _uuidSeed);
    }

    /// <summary>
    ///     读取页面主题画刷；控件尚未挂进可视树或资源缺失时使用 <paramref name="fallback" />。
    /// </summary>
    public static IBrush Brush(StyledElement element, string key, IBrush fallback)
    {
        return element.TryFindResource(key, out var value) && value is IBrush brush
            ? brush
            : fallback;
    }

    /// <summary>
    ///     把资源键解析为可参与动画插值的纯色；资源缺失或不是纯色画刷时返回 <c>false</c>。
    /// </summary>
    internal static bool TryColor(StyledElement element, string key, out NColor color)
    {
        if (element.TryFindResource(key, out var value))
            switch (value)
            {
                case ISolidColorBrush brush:
                    color = new NColor(brush.Color);
                    return true;
                case Color plain:
                    color = new NColor(plain);
                    return true;
                case NColor myColor:
                    color = myColor;
                    return true;
            }

        color = new NColor(0d, 0d, 0d, 0d);
        return false;
    }

    public static IBrush Text(StyledElement element)
    {
        return Brush(element, TextKey, Brushes.Black);
    }

    public static IBrush TextSecondary(StyledElement element)
    {
        return Brush(element, TextSecondaryKey, Brushes.Gray);
    }

    public static IBrush Accent(StyledElement element)
    {
        return Brush(element, AccentKey, Brushes.DodgerBlue);
    }

    public static IBrush AccentHover(StyledElement element)
    {
        return Brush(element, AccentHoverKey, Brushes.DeepSkyBlue);
    }

    public static IBrush AccentLight(StyledElement element)
    {
        return Brush(element, AccentLightKey, Brushes.AliceBlue);
    }

    public static IBrush ItemHover(StyledElement element)
    {
        return Brush(element, ItemHoverKey, Brushes.Transparent);
    }

    public static IBrush ItemChecked(StyledElement element)
    {
        return Brush(element, ItemCheckedKey, Brushes.AliceBlue);
    }

    public static IBrush Card(StyledElement element)
    {
        return Brush(element, CardKey, Brushes.White);
    }

    public static IBrush CardBorder(StyledElement element)
    {
        return Brush(element, CardBorderKey, Brushes.LightGray);
    }

    public static IBrush Divider(StyledElement element)
    {
        return Brush(element, DividerKey, Brushes.LightGray);
    }

    public static IBrush Error(StyledElement element)
    {
        return Brush(element, ErrorKey, Brushes.OrangeRed);
    }

    public static IBrush ErrorBackground(StyledElement element)
    {
        return Brush(element, ErrorBackgroundKey, Brushes.MistyRose);
    }

    public static IBrush DisabledBackground(StyledElement element)
    {
        return Brush(element, DisabledBackgroundKey, Brushes.Transparent);
    }

    public static IBrush IdleBorder(StyledElement element)
    {
        return Brush(element, IdleBorderKey, Brushes.LightSteelBlue);
    }

    public static IBrush IdleBackground(StyledElement element)
    {
        return Brush(element, IdleBackgroundKey, Brushes.WhiteSmoke);
    }

    public static Color ColorOf(StyledElement element, string key, Color fallback)
    {
        return TryColor(element, key, out var color) ? color : fallback;
    }

    public static Color ColorOf(IBrush? brush, Color fallback)
    {
        return brush is ISolidColorBrush solid ? solid.Color : fallback;
    }

    /// <summary>
    ///     返回同色相的半透明画刷，用于需要固定透明度的状态底色。
    /// </summary>
    public static IBrush WithAlpha(IBrush? brush, byte alpha)
    {
        if (brush is ISolidColorBrush solid)
            return new SolidColorBrush(
                Color.FromArgb(alpha, solid.Color.R, solid.Color.G, solid.Color.B));

        return Brushes.Transparent;
    }

    public static void LogDebug(Exception ex, string message)
    {
        LogWrapper.Debug(ex, "UI.Controls", message);
    }
}