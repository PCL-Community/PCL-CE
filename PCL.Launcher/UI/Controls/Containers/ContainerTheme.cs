using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

/// <summary>
///     容器类控件共用的主题取值辅助。
///     颜色统一取自 ThemeService 写入 Application.Resources 的核心 ColorBrush* 键；
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

    public static IBrush Text(StyledElement element)
    {
        return Brush(element, "ColorBrush1", Brushes.Black);
    }

    public static IBrush TextSecondary(StyledElement element)
    {
        return Brush(element, "ColorBrushGray3", Brushes.Gray);
    }

    public static IBrush Accent(StyledElement element)
    {
        return Brush(element, "ColorBrush3", Brushes.DodgerBlue);
    }

    public static IBrush AccentHover(StyledElement element)
    {
        return Brush(element, "ColorBrush4", Brushes.DeepSkyBlue);
    }

    public static IBrush AccentLight(StyledElement element)
    {
        return Brush(element, "ColorBrush7", Brushes.AliceBlue);
    }

    public static IBrush ItemHover(StyledElement element)
    {
        return Brush(element, "ColorBrushBg1", Brushes.Transparent);
    }

    public static IBrush ItemPressed(StyledElement element)
    {
        return Brush(element, "ColorBrush6", Brushes.LightBlue);
    }

    public static IBrush ItemChecked(StyledElement element)
    {
        return Brush(element, "ColorBrush7", Brushes.AliceBlue);
    }

    public static IBrush CheckedTextStrong(StyledElement element)
    {
        return Brush(element, "ColorBrush2", Brushes.DodgerBlue);
    }

    public static IBrush CheckedTextWeak(StyledElement element)
    {
        return Brush(element, "ColorBrush3", Brushes.DeepSkyBlue);
    }

    public static IBrush Divider(StyledElement element)
    {
        return Brush(element, "ColorBrushGray5", Brushes.LightGray);
    }

    public static void BindDynamic(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        _ = target.Bind(property, new DynamicResourceExtension(key));
    }
}