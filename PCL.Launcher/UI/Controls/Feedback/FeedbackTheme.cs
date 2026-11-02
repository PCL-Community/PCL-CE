using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using PCL.Core.Logging;
using PCL.UI.Controls.Common;

namespace PCL.UI.Controls;

/// <summary>
///     反馈类控件共用的主题取值辅助。
///     颜色统一取自 <c>PCL.Launcher/UI/Styles/PageStyles.axaml</c> 的 <c>PageBrush*</c> 资源，
///     保证深浅色主题切换时跟随页面配色，不在控件内硬编码页面颜色。
/// </summary>
internal static class FeedbackTheme
{
    private static readonly Uri StyleUri =
        new("avares://PCL.Launcher/UI/Styles/Controls/Feedback.axaml", UriKind.Absolute);

    private static bool _styleAttachFailed;

    /// <summary>
    ///     把反馈控件的公共样式（<c>UI/Styles/Controls/Feedback.axaml</c>）挂到控件自己的样式表上。
    ///     App.axaml 目前只引入了 PageStyles，样式表改由控件自带，避免修改他人负责的 App.axaml。
    /// </summary>
    public static void AttachStyles(StyledElement target)
    {
        if (_styleAttachFailed)
            return;

        try
        {
            target.Styles.Add(
                new StyleInclude(StyleUri)
                {
                    Source = StyleUri
                });
        }
        catch (Exception ex)
        {
            _styleAttachFailed = true;
            LogWrapper.Debug(ex, "Feedback", "[Feedback] 反馈控件样式加载失败，改用代码内默认值");
        }
    }

    /// <summary>
    ///     从资源字典读取画刷，找不到时回退到 <paramref name="fallback" />。
    /// </summary>
    public static IBrush GetBrush(StyledElement owner, string key, IBrush fallback)
    {
        return owner.TryFindResource(key, out var resource) && resource is IBrush brush
            ? brush
            : fallback;
    }

    /// <summary>
    ///     从资源字典读取画刷颜色，找不到时回退到 <paramref name="fallback" />。
    /// </summary>
    public static Color GetColor(StyledElement owner, string key, Color fallback)
    {
        return owner.TryFindResource(key, out var resource) && resource is ISolidColorBrush brush
            ? brush.Color
            : fallback;
    }

    /// <summary>
    ///     把属性绑定到 <c>PageBrush*</c> 动态资源；资源随主题切换自动刷新。
    /// </summary>
    public static void BindDynamic(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        _ = target.Bind(property, new DynamicResourceExtension(key));
    }

    /// <summary>
    ///     基于给定颜色生成附带透明度的画刷，用于提示条的浅色底。
    /// </summary>
    public static SolidColorBrush Tint(Color color, double alpha)
    {
        return new SolidColorBrush(
            Color.FromArgb(
                MathHelper.MathByte(alpha * 255d),
                color.R,
                color.G,
                color.B));
    }

    /// <summary>
    ///     当前视觉根是否为深色主题。
    /// </summary>
    public static bool IsDarkTheme(StyledElement element)
    {
        return element.ActualThemeVariant == ThemeVariant.Dark;
    }

    /// <summary>
    ///     主题变化时重新执行 <paramref name="handler" />；返回解绑方法。
    /// </summary>
    public static Action WatchTheme(StyledElement element, Action handler)
    {
        element.ActualThemeVariantChanged += OnThemeChanged;
        handler();
        return () => element.ActualThemeVariantChanged -= OnThemeChanged;

        void OnThemeChanged(object? sender, EventArgs e)
        {
            handler();
        }
    }
}