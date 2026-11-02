using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.Logging;
using MyColor = PCL.UI.Controls.Common.MyColor;

namespace PCL.UI.Controls;

/// <summary>
///     输入/选择控件共用的主题画刷解析。
///     颜色统一取自 <c>UI/Styles/PageStyles.axaml</c> 的 <c>PageBrush*</c> 动态资源；
///     控件尚未挂进可视树或资源缺失时使用调用方给出的回退画刷。
/// </summary>
internal static class InputTheme
{
    internal const string TextKey = "PageBrushText";
    internal const string TextSecondaryKey = "PageBrushTextSecondary";
    internal const string AccentKey = "PageBrushAccent";
    internal const string AccentHoverKey = "PageBrushAccentHover";
    internal const string AccentLightKey = "PageBrushAccentLight";
    internal const string ItemHoverKey = "PageBrushItemHover";
    internal const string ItemCheckedKey = "PageBrushItemChecked";
    internal const string CardKey = "PageBrushCard";
    internal const string CardBorderKey = "PageBrushCardBorder";
    internal const string DividerKey = "PageBrushDivider";
    internal const string ErrorKey = "PageBrushError";

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
    internal static bool TryColor(StyledElement element, string key, out MyColor color)
    {
        if (element.TryFindResource(key, out var value))
            switch (value)
            {
                case ISolidColorBrush brush:
                    color = new MyColor(brush.Color);
                    return true;
                case Color plain:
                    color = new MyColor(plain);
                    return true;
                case MyColor myColor:
                    color = myColor;
                    return true;
            }

        color = new MyColor(0d, 0d, 0d, 0d);
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

    public static IBrush DisabledBackground(StyledElement element)
    {
        return Brush(element, ItemHoverKey, Brushes.Transparent);
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