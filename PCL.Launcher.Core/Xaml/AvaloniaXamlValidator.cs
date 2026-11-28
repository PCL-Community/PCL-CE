using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PCL.Xaml;

/// <summary>
///     AXAML 主页的受限校验器。AXAML 通过 Avalonia 运行时加载器实例化控件，
///     因此命名空间、类型与标记扩展必须严格限制。
/// </summary>
public static partial class AvaloniaXamlValidator
{
    public const string AvaloniaNamespace = "https://github.com/avaloniaui";
    public const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private const int MaxLength = 2 * 1024 * 1024;

    private static readonly HashSet<string> ForbiddenElementNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Window",
            "TopLevel",
            "Application",
            "WebView",
            "NativeWebView",
            "ObjectDataProvider",
            "XmlDataProvider",
            "XamlReader"
        };

    private static readonly HashSet<string> ForbiddenAttributes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Click",
            "DoubleClick",
            "RightClick",
            "MouseDown",
            "MouseUp",
            "PointerPressed",
            "PointerReleased",
            "PointerMoved",
            "Tapped",
            "DoubleTapped",
            "KeyDown",
            "KeyUp",
            "TextInput",
            "Loaded",
            "Unloaded",
            "TextChanged",
            "SelectionChanged",
            "Checked",
            "Unchecked",
            "Command",
            "CommandParameter",
            "DataContext",
            "DataContextChanged"
        };

    public static AxamlValidationResult Validate(string? fragment)
    {
        var result = new AxamlValidationResult();
        if (string.IsNullOrWhiteSpace(fragment))
            return result;

        if (fragment.Length > MaxLength)
        {
            result.Fail($"XAML 内容过长（{fragment.Length} 字符，上限 {MaxLength} 字符）");
            return result;
        }

        if (fragment.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || fragment.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase))
        {
            result.Fail("不允许在 XAML 中声明 DOCTYPE 或实体");
            return result;
        }

        XDocument document;
        try
        {
            var wrapped = "<Root xmlns=\""
                + AvaloniaNamespace
                + "\""
                + " xmlns:x=\""
                + XamlNamespace
                + "\""
                + " xmlns:pclhome=\"using:PCL\">"
                + StripXmlDeclaration(fragment)
                + "</Root>";
            document = XDocument.Parse(wrapped, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            result.Fail("AXAML 解析失败：" + ex.Message);
            return result;
        }

        if (document.Root is null)
        {
            result.Fail("AXAML 内容为空");
            return result;
        }

        result.ElementCount = document.Root.Elements().Count();
        foreach (var element in document.Root.DescendantsAndSelf())
            ValidateElement(element, result);

        return result;
    }

    private static void ValidateElement(XElement element, AxamlValidationResult result)
    {
        if (element.Name == XName.Get("Root", AvaloniaNamespace))
            return;

        var localName = element.Name.LocalName;
        var namespaceName = element.Name.NamespaceName;

        if (localName.Contains('.'))
        {
            ValidateAttributes(element, result);
            return;
        }

        if (namespaceName == XamlNamespace)
        {
            if (!localName.Equals("Array", StringComparison.Ordinal))
                result.Fail($"不允许的 XAML 类型：x:{localName}");
            return;
        }

        if (IsSystemString(namespaceName, localName))
        {
            ValidateAttributes(element, result);
            return;
        }

        if (ForbiddenElementNames.Contains(localName))
        {
            result.Fail($"不允许的元素类型：{localName}");
            return;
        }

        if (!IsAllowedNamespace(namespaceName))
        {
            result.Fail($"不允许的命名空间：{namespaceName}（元素 {localName}）");
            return;
        }

        if (localName.Equals("ResourceDictionary", StringComparison.OrdinalIgnoreCase)
            && element
                .Attributes()
                .Any(attribute =>
                    attribute.Name.LocalName.Equals("Source", StringComparison.OrdinalIgnoreCase)))
        {
            result.Fail("不允许加载外部资源字典");
            return;
        }

        ValidateAttributes(element, result);
    }

    private static void ValidateAttributes(XElement element, AxamlValidationResult result)
    {
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
                continue;

            var attributeNamespace = attribute.Name.NamespaceName;
            var localName = attribute.Name.LocalName;

            if (attributeNamespace == XamlNamespace)
            {
                if (localName is "Class" or "Static" or "DataType")
                    result.Fail($"不允许的 XAML 属性：x:{localName}");
                continue;
            }

            if (attributeNamespace.Length > 0
                && attributeNamespace != AvaloniaNamespace)
            {
                if (IsCustomEventAttribute(localName)
                    && attributeNamespace.StartsWith("using:PCL", StringComparison.Ordinal))
                    continue;

                result.Fail($"不允许的属性命名空间：{attribute.Name}");
                continue;
            }

            if (ForbiddenAttributes.Contains(localName)
                || localName.StartsWith("On", StringComparison.Ordinal)
                || localName.EndsWith("Click", StringComparison.OrdinalIgnoreCase)
                || localName.EndsWith("Changed", StringComparison.OrdinalIgnoreCase))
                if (!IsCustomEventAttribute(localName))
                    result.Fail($"不允许的事件或命令属性：{localName}");

            if (localName is "Source"
                or "ImageSource"
                or "BaseUri"
                or "UriSource"
                or "FallbackSource"
                or "LoadingSource"
                or "Logo")
                ValidateSource(attribute.Value, localName, result);

            ValidateMarkupExtension(attribute.Value, localName, result);
        }
    }

    private static void ValidateMarkupExtension(
        string value,
        string attributeName,
        AxamlValidationResult result)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith('{'))
            return;

        var match = MarkupExtensionPattern().Match(trimmed);
        if (!match.Success)
        {
            result.Fail($"不允许的标记扩展（{attributeName}=\"{value}\"）");
            return;
        }

        var extension = match.Groups[1].Value;
        if (extension is not ("DynamicResource" or "StaticResource" or "TemplateBinding"))
            result.Fail($"不允许的标记扩展：{extension}");
    }

    private static void ValidateSource(
        string value,
        string attributeName,
        AxamlValidationResult result)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("{DynamicResource", StringComparison.Ordinal)
            || trimmed.StartsWith("{StaticResource", StringComparison.Ordinal))
            return;

        if (IsSafeSource(value))
            return;

        result.Fail(
            value.Contains("..", StringComparison.Ordinal)
                ? $"不允许的路径穿越（{attributeName}=\"{value}\"）"
                : $"不允许的本地或外部资源地址（{attributeName}=\"{value}\"）");
    }

    private static bool IsAllowedNamespace(string namespaceName)
    {
        return namespaceName is "" or AvaloniaNamespace
            || namespaceName.StartsWith("using:PCL", StringComparison.Ordinal)
            || namespaceName.StartsWith("using:Avalonia", StringComparison.Ordinal)
            || namespaceName.StartsWith("clr-namespace:PCL", StringComparison.Ordinal)
            || namespaceName.StartsWith("clr-namespace:Avalonia", StringComparison.Ordinal);
    }

    /// <summary>判断一个图片/资源地址是否允许在运行时主页中使用。</summary>
    public static bool IsSafeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return true;

        var value = source.Trim();
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || (!value.Contains("..", StringComparison.Ordinal)
                && (value.StartsWith("/images/", StringComparison.Ordinal)
                    || value.StartsWith(
                        "avares://PCL.Launcher/Assets/",
                        StringComparison.Ordinal)));
    }

    private static bool IsSystemString(string namespaceName, string localName)
    {
        return localName.Equals("String", StringComparison.OrdinalIgnoreCase)
            && (namespaceName.StartsWith("clr-namespace:System", StringComparison.Ordinal)
                || namespaceName.StartsWith("using:System", StringComparison.Ordinal));
    }

    private static bool IsCustomEventAttribute(string localName)
    {
        return localName is "EventType" or "EventData"
            || localName.EndsWith(".EventType", StringComparison.OrdinalIgnoreCase)
            || localName.EndsWith(".EventData", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripXmlDeclaration(string xaml)
    {
        var trimmed = xaml.TrimStart();
        if (!trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return xaml;

        var end = trimmed.IndexOf("?>", StringComparison.Ordinal);
        return end < 0 ? xaml : trimmed[(end + 2)..];
    }

    [GeneratedRegex(@"^\{([A-Za-z_][\w.:]*)\s*([^{}]*)\}$", RegexOptions.Compiled)]
    private static partial Regex MarkupExtensionPattern();
}