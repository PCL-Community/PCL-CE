using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PCL.Xaml;

/// <summary>自定义主页使用的 XAML 方言。</summary>
public enum HomepageXamlFormat
{
    /// <summary>为 WPF 版 PCL 编写的主页片段。</summary>
    Wpf,

    /// <summary>使用 Avalonia 命名空间的 AXAML 片段。</summary>
    Avalonia
}

/// <summary>根据内容而不是文件扩展名判断主页 XAML 方言。</summary>
public static partial class HomepageXamlFormatDetector
{
    public static HomepageXamlFormat Detect(string? xaml)
    {
        return string.IsNullOrWhiteSpace(xaml)
            || (!xaml.Contains(
                    XamlSecurityValidator.AvaloniaNamespace,
                    StringComparison.OrdinalIgnoreCase)
                && !AvaloniaUsingNamespace().IsMatch(xaml)
                && !xaml.Contains("pclcontrols:", StringComparison.OrdinalIgnoreCase)
                && !xaml.Contains("pclhome:", StringComparison.OrdinalIgnoreCase))
                ? HomepageXamlFormat.Wpf
                : HomepageXamlFormat.Avalonia;
    }

    [GeneratedRegex(
        @"xmlns(?::[A-Za-z_][\w.-]*)?\s*=\s*""using:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AvaloniaUsingNamespace();

    /// <summary>判断内容是否是至少语法合法的 XAML/AXAML 片段。 主页来源可能是本地文件或任意 URL，不能仅凭“自定义主页”就假定它是 XAML。</summary>
    public static bool IsXaml(string? xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
            return false;

        try
        {
            var content = StripXmlDeclaration(xaml.TrimStart('\uFEFF'));
            var wrapped =
                "<Root xmlns=\""
                + XamlSecurityValidator.PresentationNamespace
                + "\" xmlns:x=\""
                + XamlSecurityValidator.XamlNamespace
                + "\" xmlns:local=\"clr-namespace:PCL\""
                + " xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\""
                + " xmlns:pclhome=\"using:PCL\""
                + " xmlns:pclcontrols=\"using:PCL.UI.Controls\">"
                + content
                + "</Root>";
            var document = XDocument.Parse(wrapped, LoadOptions.PreserveWhitespace);
            return document.Root?.Elements().Any() == true;
        }
        catch
        {
            return false;
        }
    }

    private static string StripXmlDeclaration(string xaml)
    {
        var trimmed = xaml.TrimStart();
        if (!trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return xaml;

        var end = trimmed.IndexOf("?>", StringComparison.Ordinal);
        return end < 0 ? xaml : trimmed[(end + 2)..];
    }
}