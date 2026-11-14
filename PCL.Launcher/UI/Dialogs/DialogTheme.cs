using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace PCL.UI.Dialogs;

/// <summary>弹窗共用的尺寸取值与主题绑定。</summary>
internal static class DialogTheme
{
    /// <summary>标题字号。</summary>
    public const double TitleFontSize = 23d;

    /// <summary>正文字号。</summary>
    public const double BodyFontSize = 15d;

    /// <summary>正文行高。</summary>
    public const double BodyLineHeight = 20d;

    /// <summary>卡片圆角。</summary>
    public const double CardCornerRadius = 7d;

    /// <summary>把属性绑定到主题资源，主题切换时自动刷新。</summary>
    public static void BindBrush(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        _ = target.Bind(property, new DynamicResourceExtension(key));
    }
}