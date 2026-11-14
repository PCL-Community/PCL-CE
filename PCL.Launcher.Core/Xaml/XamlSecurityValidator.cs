using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PCL.Xaml;

/// <summary>运行时 XAML 的安全校验结果。</summary>
public sealed class XamlSecurityResult
{
    public List<string> Errors { get; } = [];

    /// <summary>片段顶层元素数量；纯文本或 JSON 内容为 0。</summary>
    public int ElementCount { get; internal set; }

    public bool IsValid => Errors.Count == 0;

    public void Fail(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Errors.Add(message);
    }
}

/// <summary>
///     运行时 XAML 白名单校验器：只允许 PCL 控件与基础布局/文本/图形类型，
///     拒绝任意 CLR 类型、事件处理器、外部资源字典、file/UNC 路径与路径穿越。
///     本类不依赖任何 UI 框架，可供主页、帮助与测试共用。
/// </summary>
public static partial class XamlSecurityValidator
{
    public const string PresentationNamespace =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    public const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    public const string AvaloniaNamespace = "https://github.com/avaloniaui";
    private const int MaxLength = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedElementNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // 布局与基础控件
            "StackPanel",
            "Grid",
            "WrapPanel",
            "DockPanel",
            "Canvas",
            "Border",
            "Viewbox",
            "ScrollViewer",
            "MyScrollViewer",
            "ContentControl",
            "Separator",
            "TextBlock",
            "Run",
            "Span",
            "Bold",
            "Italic",
            "Underline",
            "LineBreak",
            "Hyperlink",
            "Image",
            "MyImage",
            "Rectangle",
            "Ellipse",
            "Line",
            "Path",
            "TextBox",
            "FlowDocumentScrollViewer",
            "FlowDocument",
            "Paragraph",
            "List",
            "ListItem",
            "Section",
            "ControlTemplate",
            "Button",
            "MyButton",
            "MyTextButton",
            "MyIconButton",
            "MyIconTextButton",
            "MyExtraButton",
            "MyExtraTextButton",
            "MyCollapseBar",
            "MyCard",
            "MyHint",
            "MyListItem",
            "MyLoading",
            "MyTextBox",
            "MyPasswordBox",
            "MyComboBox",
            "MyComboBoxItem",
            "ComboBoxItem",
            "MyCheckBox",
            "MyRadioBox",
            "MyRadioButton",
            "MySlider",
            "MySearchBox",

            // 布局定义与本地样式（不含任何外部资源）
            "RowDefinition",
            "ColumnDefinition",
            "Style",
            "Setter",
            "ResourceDictionary",

            // 安全的画刷/效果/变换：渲染器会忽略它们，但允许出现在 Background 等属性元素中
            "SolidColorBrush",
            "LinearGradientBrush",
            "RadialGradientBrush",
            "ImageBrush",
            "GradientStop",
            "DropShadowEffect",
            "BlurEffect",
            "ScaleTransform",
            "TranslateTransform",
            "RotateTransform",
            "SkewTransform",
            "TransformGroup",
            "MatrixTransform",
            "Color",
            "Thickness",
            "Point",
            "Vector",
            "Rect",
            "Size",
            "String"
        };

    private static readonly HashSet<string> AllowedPropertyElementNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Resources",
            "Inlines",
            "Title",
            "Content",
            "Children",
            "Text",
            "Setters",
            "Value",
            "RowDefinitions",
            "ColumnDefinitions",
            "Buttons",
            "Items",
            "Background",
            "Foreground",
            "BorderBrush",
            "BorderThickness",
            "CornerRadius",
            "Effect",
            "Child",
            "RenderTransform",
            "Data",
            "Geometry",
            "Fill",
            "Stroke",
            "Clip",
            "ToolTip",
            "Tip",
            "Transitions",
            "Styles",
            "Source",
            "ImageSource",
            "Width",
            "Height",
            "MinWidth",
            "MaxWidth",
            "MinHeight",
            "MaxHeight",
            "Margin",
            "Padding",
            "Opacity",
            "FontSize",
            "FontWeight",
            "TextWrapping",
            "Stretch",
            "Orientation",
            "Spacing",
            "HorizontalAlignment",
            "VerticalAlignment",
            "IsVisible",
            "IsEnabled",
            "StartPoint",
            "EndPoint",
            "GradientStops",
            "Stops",
            "RadiusX",
            "RadiusY",
            "Center",
            "GradientOrigin",
            "Radius",
            "MappingMode",
            "SpreadMethod",
            "Transform",
            "Triggers",
            "Template"
        };

    private static readonly HashSet<string> AllowedXamlAttributes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Key",
            "Name",
            "Uid",
            "FieldModifier",
            "SharedSizeGroup",
            "Shared"
        };

    private static readonly HashSet<string> ForbiddenEventAttributes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Click",
            "DoubleClick",
            "RightClick",
            "MouseDown",
            "MouseUp",
            "MouseEnter",
            "MouseLeave",
            "MouseMove",
            "MouseWheel",
            "PointerPressed",
            "PointerReleased",
            "PointerMoved",
            "PointerEntered",
            "PointerExited",
            "PointerWheelChanged",
            "Tapped",
            "DoubleTapped",
            "RightTapped",
            "Holding",
            "KeyDown",
            "KeyUp",
            "TextInput",
            "GotFocus",
            "LostFocus",
            "Loaded",
            "Unloaded",
            "Checked",
            "Unchecked",
            "SelectionChanged",
            "TextChanged",
            "ValueChanged",
            "Command",
            "CommandParameter",
            "Submit",
            "Drop",
            "DragEnter",
            "DragOver",
            "DragLeave",
            "LayoutUpdated",
            "SizeChanged",
            "DataContextChanged",
            "PropertyChanged"
        };

    /// <summary>校验一段可被包裹在根节点中的 XAML 片段。</summary>
    public static XamlSecurityResult ValidateFragment(string? fragment)
    {
        var result = new XamlSecurityResult();
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
                + PresentationNamespace
                + "\""
                + " xmlns:x=\""
                + XamlNamespace
                + "\""
                + " xmlns:local=\"clr-namespace:PCL\""
                + " xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\">"
                + StripXmlDeclaration(fragment)
                + "</Root>";
            document = XDocument.Parse(wrapped, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            result.Fail("XAML 解析失败：" + ex.Message);
            return result;
        }

        if (document.Root is null)
        {
            result.Fail("XAML 内容为空");
            return result;
        }

        result.ElementCount = document.Root.Elements().Count();
        foreach (var element in document.Root.DescendantsAndSelf())
            ValidateElement(element, result);

        return result;
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
                && ((value.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)
                        && value.Contains("/images/", StringComparison.OrdinalIgnoreCase))
                    || value.StartsWith("/images/", StringComparison.Ordinal)
                    || value.StartsWith(
                        "avares://PCL.Launcher/Assets/",
                        StringComparison.Ordinal)));
    }

    private static void ValidateElement(XElement element, XamlSecurityResult result)
    {
        var localName = element.Name.LocalName;
        var namespaceName = element.Name.NamespaceName;

        if (element.Name == XName.Get("Root", PresentationNamespace))
            return;

        if (localName.Contains('.'))
        {
            if (!IsAllowedPropertyElement(localName))
                result.Fail($"不允许的属性元素：{localName}");
        }
        else
        {
            if (namespaceName == XamlNamespace)
            {
                if (localName.Equals("Array", StringComparison.Ordinal)
                    && IsAllowedXArray(element))
                {
                    ValidateAttributes(element, result);
                    return;
                }

                result.Fail($"不允许的 XAML 类型：x:{localName}");
                return;
            }

            if (!AllowedElementNames.Contains(localName))
            {
                result.Fail($"不允许的元素类型：{localName}");
                return;
            }

            if (localName.Equals("String", StringComparison.OrdinalIgnoreCase)
                && IsSystemStringNamespace(namespaceName))
            {
                ValidateAttributes(element, result);
                return;
            }

            if (localName.Equals("ResourceDictionary", StringComparison.OrdinalIgnoreCase)
                && element
                    .Attributes()
                    .Any(attribute =>
                        attribute.Name.LocalName.Equals(
                            "Source",
                            StringComparison.OrdinalIgnoreCase)))
            {
                result.Fail("不允许加载外部资源字典");
                return;
            }

            if (!IsAllowedNamespace(namespaceName))
                result.Fail($"不允许的命名空间：{namespaceName}（元素 {localName}）");
        }

        ValidateAttributes(element, result);
    }

    private static void ValidateAttributes(XElement element, XamlSecurityResult result)
    {
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
                continue;

            var attributeNamespace = attribute.Name.NamespaceName;
            var localName = attribute.Name.LocalName;

            if (attributeNamespace == XamlNamespace)
            {
                if (element.Name == XName.Get("Array", XamlNamespace)
                    && localName.Equals("Type", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!AllowedXamlAttributes.Contains(localName))
                    result.Fail($"不允许的 XAML 属性：x:{localName}");
                continue;
            }

            if (attributeNamespace == "http://schemas.openxmlformats.org/markup-compatibility/2006"
                || attributeNamespace.StartsWith(
                    "http://schemas.microsoft.com/expression/blend",
                    StringComparison.Ordinal))
                continue;

            if (attributeNamespace.Length > 0 && attributeNamespace != PresentationNamespace)
            {
                if (attributeNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
                {
                    if (!IsAllowedCustomEventAttribute(localName))
                        result.Fail($"不允许的 CLR 附加属性：{attribute.Name}");
                    continue;
                }

                result.Fail($"不允许的属性命名空间：{attribute.Name}");
                continue;
            }

            if (IsForbiddenEventAttribute(localName))
                result.Fail($"不允许的事件或命令属性：{localName}");

            if (localName is "Source"
                or "ImageSource"
                or "BaseUri"
                or "UriSource"
                or "FallbackSource"
                or "LoadingSource")
                ValidateSource(attribute.Value, localName, result);
            if (localName.Equals("Logo", StringComparison.OrdinalIgnoreCase)
                && element.Name.LocalName.Equals("MyListItem", StringComparison.OrdinalIgnoreCase))
                ValidateSource(attribute.Value, localName, result);

            ValidateMarkupExtension(attribute.Value, localName, result);

            if (element.Name.LocalName != "Setter"
                || !localName.Equals("Property", StringComparison.OrdinalIgnoreCase))
                continue;

            if (attribute.Value.Contains("Command", StringComparison.OrdinalIgnoreCase))
                result.Fail("不允许通过 Style Setter 注入命令属性");

            if (!attribute.Value.EndsWith("Source", StringComparison.OrdinalIgnoreCase)
                && !attribute.Value.Equals("Logo", StringComparison.OrdinalIgnoreCase))
                continue;

            var setterValue = element
                .Attributes()
                .FirstOrDefault(item =>
                    item.Name.LocalName.Equals("Value", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            if (setterValue is not null)
                ValidateSource(setterValue, "Setter.Value", result);
        }
    }

    private static bool IsAllowedXArray(XElement element)
    {
        var typeValue = element
            .Attributes()
            .FirstOrDefault(attribute =>
                attribute.Name.LocalName.Equals("Type", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return !string.IsNullOrWhiteSpace(typeValue)
            && TryGetXTypeName(typeValue.Trim(), out var typeName)
            && AllowedElementNames.Contains(StripClrPrefix(typeName));
    }

    private static bool IsAllowedPropertyElement(string localName)
    {
        var separator = localName.IndexOf('.');
        if (separator <= 0 || separator == localName.Length - 1)
            return false;

        var owner = localName[..separator];
        var property = localName[(separator + 1)..];
        return AllowedElementNames.Contains(owner)
            && AllowedPropertyElementNames.Contains(property);
    }

    private static bool IsAllowedNamespace(string namespaceName)
    {
        if (namespaceName.Length == 0 || namespaceName == PresentationNamespace)
            return true;
        if (namespaceName == XamlNamespace)
            return true;
        if (!namespaceName.StartsWith("clr-namespace:", StringComparison.Ordinal))
            return false;

        var clrNamespace = namespaceName["clr-namespace:".Length..];
        var assemblyIndex = clrNamespace.IndexOf(';');
        if (assemblyIndex >= 0)
            clrNamespace = clrNamespace[..assemblyIndex];
        return clrNamespace is "PCL" or "PCL2";
    }

    private static bool IsAllowedCustomEventAttribute(string localName)
    {
        return localName.Equals("CustomEventService.EventType", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("CustomEventService.EventData", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("EventType", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("EventData", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenEventAttribute(string localName)
    {
        return ForbiddenEventAttributes.Contains(localName)
            || (localName.Length > 2
                && localName.StartsWith("On", StringComparison.Ordinal)
                && char.IsUpper(localName[2]))
            || localName.Contains("Command", StringComparison.OrdinalIgnoreCase)
            || localName.EndsWith("Click", StringComparison.OrdinalIgnoreCase)
            || localName.EndsWith("Tapped", StringComparison.OrdinalIgnoreCase)
            || localName.EndsWith("Gesture", StringComparison.OrdinalIgnoreCase)
            || localName.EndsWith("Changed", StringComparison.OrdinalIgnoreCase)
            || localName.EndsWith("Pressed", StringComparison.OrdinalIgnoreCase)
            || localName.EndsWith("Released", StringComparison.OrdinalIgnoreCase)
            || localName.EndsWith("Entered", StringComparison.OrdinalIgnoreCase)
            || localName.EndsWith("Exited", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateSource(
        string value,
        string attributeName,
        XamlSecurityResult result)
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

    private static void ValidateMarkupExtension(
        string value,
        string attributeName,
        XamlSecurityResult result)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith('{'))
            return;

        if (attributeName.Equals("TargetType", StringComparison.OrdinalIgnoreCase)
            && TryGetXTypeName(trimmed, out var targetType)
            && AllowedElementNames.Contains(StripClrPrefix(targetType)))
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

    private static bool IsSystemStringNamespace(string namespaceName)
    {
        return namespaceName.StartsWith("clr-namespace:System", StringComparison.Ordinal)
            && (namespaceName.Contains("System.Runtime", StringComparison.Ordinal)
                || namespaceName.Contains("mscorlib", StringComparison.Ordinal)
                || namespaceName.Equals("clr-namespace:System", StringComparison.Ordinal));
    }

    private static bool TryGetXTypeName(string value, out string typeName)
    {
        typeName = "";
        var match = Regex.Match(value, @"^\{x:Type\s+([^{}]+)\}$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;
        typeName = match.Groups[1].Value.Trim();
        return typeName.Length > 0;
    }

    private static string StripClrPrefix(string typeName)
    {
        var separator = typeName.LastIndexOf(':');
        return separator >= 0 ? typeName[(separator + 1)..] : typeName;
    }

    private static string StripXmlDeclaration(string xaml)
    {
        var trimmed = xaml.TrimStart();
        if (!trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return xaml;

        var end = trimmed.IndexOf("?>", StringComparison.Ordinal);
        return end < 0 ? xaml : trimmed[(end + 2)..];
    }

    [GeneratedRegex(@"^\{([A-Za-z_][\w.:]*)\s+([^{}]*)\}$", RegexOptions.Compiled)]
    private static partial Regex MarkupExtensionPattern();
}