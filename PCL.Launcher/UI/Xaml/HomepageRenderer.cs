using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.UI.Controls;
using PCL.Xaml;

namespace PCL.UI.Xaml;

/// <summary>主页 AXAML 的渲染结果。</summary>
public sealed class HomepageRenderResult
{
    public Control? Root { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// 主页加载器。主页只接受 AXAML，使用 Avalonia 原生运行时加载器，以便 Style、Selector、ControlTemplate 和资源系统按
/// Avalonia 的规则工作。输入先经过受限校验和命名空间/事件属性归一化，避免任意 CLR 类型进入运行时加载器。
/// </summary>
public static class HomepageRenderer
{
    private const string AvaloniaNamespace = "https://github.com/avaloniaui";
    private const string PclNamespacePrefix = "pclhome";
    private const string PclNamespace = "using:PCL";
    private const string PclControlsNamespacePrefix = "pclcontrols";
    private const string PclControlsNamespace = "using:PCL.UI.Controls";

    public static HomepageRenderResult Render(string? xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
            return new HomepageRenderResult
            {
                Error = "AXAML 内容为空"
            };

        var fragment = EnsurePclControlsNamespace(xaml);
        var validation = AvaloniaXamlValidator.Validate(fragment);
        if (!validation.IsValid)
            return new HomepageRenderResult
            {
                Error = string.Join("\n", validation.Errors)
            };

        try
        {
            var normalized = Normalize(fragment);
            var loaded = AvaloniaRuntimeXamlLoader.Load(
                normalized,
                typeof(MyCard).Assembly);
            if (loaded is not Control root)
                return new HomepageRenderResult
                {
                    Error = "AXAML 根节点不是可显示的控件"
                };

            return new HomepageRenderResult
            {
                Root = root
            };
        }
        catch (Exception ex)
        {
            return new HomepageRenderResult
            {
                Error = ex.Message
            };
        }
    }

    /// <summary>渲染一段 AXAML 片段；片段会被包进一个 <see cref="StackPanel" />，用于替换面板子内容。</summary>
    public static HomepageRenderResult RenderFragment(string? fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
            return new HomepageRenderResult
            {
                Root = new StackPanel()
            };

        return Render(
            "<StackPanel xmlns=\""
            + AvaloniaNamespace
            + "\">"
            + StripXmlDeclaration(fragment)
            + "</StackPanel>");
    }

    private static string Normalize(string xaml)
    {
        var document = XDocument.Parse(
            StripXmlDeclaration(xaml),
            LoadOptions.PreserveWhitespace);
        var root = document.Root
            ?? throw new InvalidDataException("AXAML 根节点为空");

        if (root.GetDefaultNamespace() == XNamespace.None)
        {
            var avaloniaNamespace = XNamespace.Get(AvaloniaNamespace);
            foreach (var element in root.DescendantsAndSelf())
                if (element.Name.Namespace == XNamespace.None)
                    element.Name = avaloniaNamespace + element.Name.LocalName;
            root.SetAttributeValue("xmlns", AvaloniaNamespace);
        }

        root.SetAttributeValue(XNamespace.Xmlns + PclNamespacePrefix, PclNamespace);
        root.SetAttributeValue(
            XNamespace.Xmlns + PclControlsNamespacePrefix,
            PclControlsNamespace);
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

    private static string EnsurePclControlsNamespace(string xaml)
    {
        if (xaml.Contains("xmlns:pclcontrols", StringComparison.Ordinal)
            || !xaml.Contains("pclcontrols:", StringComparison.Ordinal))
            return xaml;

        var match = Regex.Match(xaml, @"<([A-Za-z_][A-Za-z0-9_.:-]*)\b");
        return !match.Success
            ? xaml
            : xaml.Insert(
                match.Index + match.Length,
                " xmlns:pclcontrols=\"using:PCL.UI.Controls\"");
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