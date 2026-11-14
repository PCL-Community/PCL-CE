using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.UI.Controls;
using PCL.Xaml;

namespace PCL.UI.Xaml;

/// <summary>AXAML 主页的渲染结果。</summary>
public sealed class HomepageAvaloniaRenderResult
{
    public Control? Root { get; init; }

    public string? Error { get; init; }

    public List<string> Warnings { get; } = [];
}

/// <summary>
///     AXAML 主页加载器。AXAML 使用 Avalonia 原生运行时加载器，
///     以便 Style、Selector、ControlTemplate 和资源系统按 Avalonia 的规则工作。
///     输入先经过受限校验和命名空间/事件属性归一化，避免任意 CLR 类型进入运行时加载器。
/// </summary>
public static class HomepageAvaloniaRenderer
{
    private const string PclNamespacePrefix = "pclhome";
    private const string PclNamespace = "using:PCL";

    public static HomepageAvaloniaRenderResult Render(string? xaml)
    {
        _ = new HomepageAvaloniaRenderResult();
        if (string.IsNullOrWhiteSpace(xaml))
            return new HomepageAvaloniaRenderResult
            {
                Error = "AXAML 内容为空"
            };

        var validation = AvaloniaXamlValidator.Validate(xaml);
        if (!validation.IsValid)
            return new HomepageAvaloniaRenderResult
            {
                Error = string.Join("\n", validation.Errors)
            };

        try
        {
            var normalized = Normalize(xaml);
            var loaded = AvaloniaRuntimeXamlLoader.Load(
                normalized,
                typeof(MyCard).Assembly);
            if (loaded is not Control root)
                return new HomepageAvaloniaRenderResult
                {
                    Error = "AXAML 根节点不是可显示的控件"
                };

            return new HomepageAvaloniaRenderResult
            {
                Root = root
            };
        }
        catch (Exception ex)
        {
            return new HomepageAvaloniaRenderResult
            {
                Error = ex.Message
            };
        }
    }

    private static string Normalize(string xaml)
    {
        var document = XDocument.Parse(
            StripXmlDeclaration(xaml),
            LoadOptions.PreserveWhitespace);
        var root = document.Root
            ?? throw new InvalidDataException("AXAML 根节点为空");

        root.SetAttributeValue(XNamespace.Xmlns + PclNamespacePrefix, PclNamespace);
        NormalizeAttributes(root);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static void NormalizeAttributes(XElement root)
    {
        foreach (var element in root.DescendantsAndSelf())
        {
            NormalizeEventAttribute(element, "EventType");
            NormalizeEventAttribute(element, "EventData");

            if (element.Name.LocalName.Equals("Image", StringComparison.OrdinalIgnoreCase)
                || element.Name.LocalName.Equals("MyImage", StringComparison.OrdinalIgnoreCase)
                || element.Name.LocalName.Equals("ImageBrush", StringComparison.OrdinalIgnoreCase))
            {
                var imageSource = element.Attribute("ImageSource");
                if (imageSource is not null && element.Attribute("Source") is null)
                {
                    imageSource.Remove();
                    element.SetAttributeValue("Source", imageSource.Value);
                }
            }

            var visibility = element.Attribute("Visibility");
            if (visibility is null || element.Attribute("IsVisible") is not null)
                continue;

            visibility.Remove();
            element.SetAttributeValue(
                "IsVisible",
                visibility.Value.Equals("Visible", StringComparison.OrdinalIgnoreCase)
                    ? "True"
                    : "False");
        }
    }

    private static void NormalizeEventAttribute(XElement element, string name)
    {
        var attribute = element.Attribute(name);
        if (attribute is null)
            return;

        var value = attribute.Value;
        if (name == "EventType")
        {
            if (EventTypeMapper.TryToEnglish(value, out var englishName))
                value = englishName;
            else if (Enum.TryParse<EventType>(value, true, out var eventType))
                value = eventType.ToString();
        }

        element.SetAttributeValue(
            XName.Get(
                $"CustomEventService.{name}",
                PclNamespace),
            value);
        attribute.Remove();
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