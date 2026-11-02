using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

/// <summary>
///     容器类控件共用的主题取值辅助。
///     颜色统一取自 UI/Styles/PageStyles.axaml 中的 PageBrush* 资源；
///     样式表放在 UI/Styles/Controls/Containers.axaml，需要视觉默认值的控件在构造函数中调用
///     <see cref="AttachStyles" /> 引入。
/// </summary>
internal static class ContainerTheme
{
    private static readonly Uri StyleUri =
        new("avares://PCL.Launcher/UI/Styles/Controls/Containers.axaml", UriKind.Absolute);

    private static bool _styleAttachFailed;

    /// <summary>
    ///     把容器的公共样式表挂到控件自己的样式表上。
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
            LogWrapper.Debug(ex, "UI.Controls", "[Containers] 容器控件样式加载失败，改用代码内默认值");
        }
    }

    /// <summary>读取页面主题画刷；资源缺失时回退到 <paramref name="fallback" />。</summary>
    public static IBrush Brush(StyledElement element, string key, IBrush fallback)
    {
        return element.TryFindResource(key, out var resource) && resource is IBrush brush
            ? brush
            : fallback;
    }

    /// <summary>主文本颜色（PageBrushText）。</summary>
    public static IBrush Text(StyledElement element)
    {
        return Brush(element, "PageBrushText", Brushes.Black);
    }

    /// <summary>次级文本颜色（PageBrushTextSecondary）。</summary>
    public static IBrush TextSecondary(StyledElement element)
    {
        return Brush(element, "PageBrushTextSecondary", Brushes.Gray);
    }

    /// <summary>强调色（PageBrushAccent）。</summary>
    public static IBrush Accent(StyledElement element)
    {
        return Brush(element, "PageBrushAccent", Brushes.DodgerBlue);
    }

    /// <summary>强调色悬停态（PageBrushAccentHover）。</summary>
    public static IBrush AccentHover(StyledElement element)
    {
        return Brush(element, "PageBrushAccentHover", Brushes.DeepSkyBlue);
    }

    /// <summary>浅强调色背景（PageBrushAccentLight）。</summary>
    public static IBrush AccentLight(StyledElement element)
    {
        return Brush(element, "PageBrushAccentLight", Brushes.AliceBlue);
    }

    /// <summary>列表项悬停底色（PageBrushItemHover）。</summary>
    public static IBrush ItemHover(StyledElement element)
    {
        return Brush(element, "PageBrushItemHover", Brushes.Transparent);
    }

    /// <summary>选中项底色（PageBrushItemChecked）。</summary>
    public static IBrush ItemChecked(StyledElement element)
    {
        return Brush(element, "PageBrushItemChecked", Brushes.AliceBlue);
    }

    /// <summary>分隔线与弱化描边（PageBrushDivider）。</summary>
    public static IBrush Divider(StyledElement element)
    {
        return Brush(element, "PageBrushDivider", Brushes.LightGray);
    }

    /// <summary>把属性绑定到 PageBrush* 动态资源；资源随主题切换自动刷新。</summary>
    public static void BindDynamic(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        _ = target.Bind(property, new DynamicResourceExtension(key));
    }
}