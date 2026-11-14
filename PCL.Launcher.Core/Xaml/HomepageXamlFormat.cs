using System.Text.RegularExpressions;

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
                && !AvaloniaUsingNamespace().IsMatch(xaml))
                ? HomepageXamlFormat.Wpf
                : HomepageXamlFormat.Avalonia;
    }

    [GeneratedRegex(
        @"xmlns(?::[A-Za-z_][\w.-]*)?\s*=\s*""using:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AvaloniaUsingNamespace();
}