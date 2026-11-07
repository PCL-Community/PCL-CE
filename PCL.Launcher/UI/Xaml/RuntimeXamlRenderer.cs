using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.UI.Controls;
using PCL.Xaml;

namespace PCL.UI.Xaml;

/// <summary>运行时 XAML 渲染选项。</summary>
public sealed class RuntimeXamlRenderOptions
{
    /// <summary>是否读取并应用内联 <c>Resources/Style</c>；默认开启。</summary>
    public bool ApplyLocalStyles { get; init; } = true;
}

/// <summary>运行时 XAML 渲染结果。</summary>
public sealed class RuntimeXamlRenderResult
{
    /// <summary>渲染出的顶层控件，按原 XAML 顺序排列。</summary>
    public List<Control> Controls { get; } = [];

    /// <summary>已移除的不受支持事件类型（例如 CE 不再提供的功能）。</summary>
    public List<string> UnsupportedEventTypes { get; } = [];

    /// <summary>无法识别的自定义事件类型。</summary>
    public List<string> UnrecognizedEventTypes { get; } = [];

    /// <summary>非致命提示（例如忽略了未知的样式属性）。</summary>
    public List<string> Warnings { get; } = [];

    public bool Success { get; internal set; }

    public string? Error { get; internal set; }
}

/// <summary>
///     白名单运行时 XAML 渲染器：先用 <see cref="XamlSecurityValidator" /> 校验，
///     再自行把受支持的 PCL/基础控件映射为 Avalonia 控件。不使用 Avalonia 运行时 XAML 加载器，
///     因此不会实例化任意 CLR 类型，也不会执行事件处理器或加载外部资源字典。
/// </summary>
public static class RuntimeXamlRenderer
{
    private static readonly Regex MarkupExtensionPattern =
        new(@"^\{([A-Za-z_][\w.:]*)\s+([^{}]*)\}$", RegexOptions.Compiled);

    private static readonly HashSet<string> IgnoredElementNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
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
            "Thickness"
        };

    public static RuntimeXamlRenderResult RenderFragment(
        string? fragment,
        Func<string, object?> resolveResource,
        RuntimeXamlRenderOptions? options = null)
    {
        var result = new RuntimeXamlRenderResult();
        if (string.IsNullOrWhiteSpace(fragment))
        {
            result.Success = true;
            return result;
        }

        var security = XamlSecurityValidator.ValidateFragment(fragment);
        if (!security.IsValid)
        {
            result.Error = string.Join("\n", security.Errors);
            return result;
        }

        try
        {
            var wrapped = "<Root xmlns=\""
                + XamlSecurityValidator.PresentationNamespace
                + "\""
                + " xmlns:x=\""
                + XamlSecurityValidator.XamlNamespace
                + "\""
                + " xmlns:local=\"clr-namespace:PCL\">"
                + StripXmlDeclaration(fragment)
                + "</Root>";
            var document = XDocument.Parse(wrapped, LoadOptions.PreserveWhitespace);
            if (document.Root is null)
            {
                result.Success = true;
                return result;
            }

            var context = new RenderContext(
                resolveResource,
                options ?? new RuntimeXamlRenderOptions(),
                result);
            foreach (var element in document.Root.Elements())
            {
                var control = context.ConvertElement(element, null);
                if (control is not null)
                    result.Controls.Add(control);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private static string StripXmlDeclaration(string xaml)
    {
        var trimmed = xaml.TrimStart();
        if (!trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return xaml;

        var end = trimmed.IndexOf("?>", StringComparison.Ordinal);
        return end < 0 ? xaml : trimmed[(end + 2)..];
    }

    private static string? FindAttribute(XElement element, string name)
    {
        return element
            .Attributes()
            .FirstOrDefault(attribute =>
                !attribute.IsNamespaceDeclaration
                && attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string? GetProp(
        XElement element,
        IReadOnlyDictionary<string, string> styles,
        string name)
    {
        var attribute = FindAttribute(element, name);
        return attribute ?? styles.GetValueOrDefault(name);
    }

    private static bool? ParseBool(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? null
            : raw.Trim().ToLowerInvariant() switch
            {
                "true" or "1" or "yes" => true,
                "false" or "0" or "no" => false,
                _ => null
            };
    }

    private static int? ParseInt(string? raw)
    {
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double? ParseDouble(string? raw)
    {
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static TEnum? ParseEnum<TEnum>(string? raw) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(raw, true, out var value) ? value : null;
    }

    private static TextTrimming? ParseTextTrimming(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "characterellipsis" or "character_ellipsis" => TextTrimming.CharacterEllipsis,
            "wordellipsis" or "word_ellipsis" => TextTrimming.WordEllipsis,
            "none" => TextTrimming.None,
            _ => null
        };
    }

    private static FontWeight ParseFontWeight(string raw)
    {
        return raw.Trim() switch
        {
            "Bold" => FontWeight.Bold,
            "SemiBold" or "DemiBold" => FontWeight.SemiBold,
            "Light" => FontWeight.Light,
            "ExtraLight" => FontWeight.ExtraLight,
            "Thin" => FontWeight.Thin,
            "Medium" => FontWeight.Medium,
            "ExtraBold" => FontWeight.ExtraBold,
            "Black" => FontWeight.Black,
            _ => FontWeight.Normal
        };
    }

    private static FontStyle ParseFontStyle(string raw)
    {
        return raw.Trim() switch
        {
            "Italic" => FontStyle.Italic,
            "Oblique" => FontStyle.Oblique,
            _ => FontStyle.Normal
        };
    }

    private static HorizontalAlignment? ParseHorizontalAlignment(string? raw)
    {
        return raw?.Trim() switch
        {
            "Left" => HorizontalAlignment.Left,
            "Center" => HorizontalAlignment.Center,
            "Right" => HorizontalAlignment.Right,
            "Stretch" => HorizontalAlignment.Stretch,
            _ => null
        };
    }

    private static VerticalAlignment? ParseVerticalAlignment(string? raw)
    {
        return raw?.Trim() switch
        {
            "Top" => VerticalAlignment.Top,
            "Center" => VerticalAlignment.Center,
            "Bottom" => VerticalAlignment.Bottom,
            "Stretch" => VerticalAlignment.Stretch,
            _ => null
        };
    }

    private static Thickness? ParseThickness(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var parts = raw.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            var uniform = ParseDouble(parts[0]);
            return uniform is null ? null : new Thickness(uniform.Value);
        }

        if (parts.Length != 4)
            return null;
        if (ParseDouble(parts[0]) is not { } left
            || ParseDouble(parts[1]) is not { } top
            || ParseDouble(parts[2]) is not { } right
            || ParseDouble(parts[3]) is not { } bottom)
            return null;
        return new Thickness(left, top, right, bottom);
    }

    private static CornerRadius? ParseCornerRadius(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var parts = raw.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 1 && ParseDouble(parts[0]) is { } uniform)
            return new CornerRadius(uniform);
        if (parts.Length == 4
            && ParseDouble(parts[0]) is { } topLeft
            && ParseDouble(parts[1]) is { } topRight
            && ParseDouble(parts[2]) is { } bottomRight
            && ParseDouble(parts[3]) is { } bottomLeft)
            return new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
        return null;
    }

    private static List<GridLength>? ParseGridDefinitions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return
        [
            .. raw
                .Split(',', StringSplitOptions.TrimEntries)
                .Where(part => part.Length > 0)
                .Select(ParseGridLength)
        ];
    }

    private static GridLength ParseGridLength(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return GridLength.Auto;
        if (raw.EndsWith('*'))
        {
            var star = raw[..^1];
            return double.TryParse(
                star,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var factor)
                ? new GridLength(factor, GridUnitType.Star)
                : new GridLength(1d, GridUnitType.Star);
        }

        return ParseDouble(raw) is { } pixels ? new GridLength(pixels) : GridLength.Auto;
    }

    private sealed class RenderContext(
        Func<string, object?> resolveResource,
        RuntimeXamlRenderOptions options,
        RuntimeXamlRenderResult result)
    {
        private readonly HashSet<Control> _rejectedControls = [];

        public Control? ConvertElement(XElement element, StyleScope? inheritedStyles)
        {
            var elementName = element.Name.LocalName;
            if (elementName.Contains('.'))
                return null;
            if (IgnoredElementNames.Contains(elementName))
                return null;

            var styleScope = new StyleScope(inheritedStyles);
            if (options.ApplyLocalStyles)
                CollectStyles(element, styleScope);
            var styles = styleScope.Resolve(elementName, element);

            Control? control = elementName switch
            {
                "StackPanel" => ConvertStackPanel(element, styleScope, styles),
                "WrapPanel" => ConvertWrapPanel(element, styleScope, styles),
                "DockPanel" => ConvertDockPanel(element, styleScope, styles),
                "Canvas" => ConvertCanvas(element, styleScope, styles),
                "Grid" => ConvertGrid(element, styleScope, styles),
                "Border" => ConvertBorder(element, styleScope, styles),
                "Viewbox" => ConvertViewbox(element, styleScope, styles),
                "ScrollViewer" => ConvertScrollViewer(element, styleScope, styles),
                "MyScrollViewer" => ConvertMyScrollViewer(element, styleScope, styles),
                "ContentControl" => ConvertContentControl(element, styleScope, styles),
                "TextBlock" => ConvertTextBlock(element, styles),
                "Run" or "Span" or "Bold" or "Italic" or "Hyperlink" => ConvertInlineTextBlock(
                    element,
                    styles),
                "LineBreak" => new TextBlock
                {
                    Text = "\n"
                },
                "Image" or "MyImage" => ConvertImage(element, styles),
                "Rectangle" or "Ellipse" or "Line" => ConvertShape(element, styles),
                "Path" => ConvertPath(element, styles),
                "Button" or "MyButton" => ConvertMyButton(element, styles),
                "MyTextButton" => ConvertMyTextButton(element, styles),
                "MyIconButton" => ConvertMyIconButton(element, styles),
                "MyIconTextButton" => ConvertMyIconTextButton(element, styles),
                "MyExtraButton" => ConvertMyExtraButton(element, styles),
                "MyExtraTextButton" => ConvertMyExtraTextButton(element, styles),
                "MyCollapseBar" => ConvertMyCollapseBar(element, styleScope, styles),
                "MyCard" => ConvertMyCard(element, styleScope, styles),
                "MyHint" => ConvertMyHint(element, styleScope, styles),
                "MyListItem" => ConvertMyListItem(element, styles),
                "MyLoading" => ConvertMyLoading(element, styles),
                "MyTextBox" => ConvertMyTextBox(element, styles),
                "MyPasswordBox" => ConvertMyPasswordBox(element, styles),
                "MyComboBox" => ConvertMyComboBox(element, styleScope, styles),
                "MyComboBoxItem" or "ComboBoxItem" => ConvertMyComboBoxItem(
                    element,
                    styleScope,
                    styles),
                "MyCheckBox" => ConvertMyCheckBox(element, styles),
                "MyRadioBox" => ConvertMyRadioBox(element, styles),
                "MyRadioButton" => ConvertMyRadioButton(element, styles),
                "MySlider" => ConvertMySlider(element, styles),
                "MySearchBox" => ConvertMySearchBox(element, styles),
                "Separator" => ConvertSeparator(element, styles),
                _ => null
            };

            if (control is null
                && !elementName.Contains('.')
                && !IgnoredElementNames.Contains(elementName))
                result.Warnings.Add($"不支持的元素：{elementName}");

            if (control is not null && _rejectedControls.Remove(control))
                return null;
            return control;
        }

        private void ApplyCommon(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseThickness(GetProp(element, styles, "Margin")) is { } margin)
                control.Margin = margin;
            if (ParseDouble(GetProp(element, styles, "Width")) is { } width and > 0d)
                control.Width = width;
            if (ParseDouble(GetProp(element, styles, "Height")) is { } height and > 0d)
                control.Height = height;
            if (ParseDouble(GetProp(element, styles, "MinWidth")) is { } minWidth and >= 0d)
                control.MinWidth = minWidth;
            if (ParseDouble(GetProp(element, styles, "MaxWidth")) is { } maxWidth and > 0d)
                control.MaxWidth = maxWidth;
            if (ParseDouble(GetProp(element, styles, "MinHeight")) is { } minHeight and >= 0d)
                control.MinHeight = minHeight;
            if (ParseDouble(GetProp(element, styles, "MaxHeight")) is { } maxHeight and > 0d)
                control.MaxHeight = maxHeight;
            if (ParseDouble(GetProp(element, styles, "Opacity")) is { } opacity)
                control.Opacity = Math.Clamp(opacity, 0d, 1d);

            control.HorizontalAlignment = ParseHorizontalAlignment(
                    GetProp(element, styles, "HorizontalAlignment"))
                ?? control.HorizontalAlignment;
            control.VerticalAlignment = ParseVerticalAlignment(
                    GetProp(element, styles, "VerticalAlignment"))
                ?? control.VerticalAlignment;

            if (ParseBool(GetProp(element, styles, "IsVisible")) is { } isVisible)
                control.IsVisible = isVisible;
            else if (GetProp(element, styles, "Visibility") is { } visibility)
                control.IsVisible = !visibility.Equals(
                        "Collapsed",
                        StringComparison.OrdinalIgnoreCase)
                    && !visibility.Equals("Hidden", StringComparison.OrdinalIgnoreCase);
            if (ParseBool(GetProp(element, styles, "IsEnabled")) is { } isEnabled)
                control.IsEnabled = isEnabled;
            if (ParseBool(GetProp(element, styles, "IsHitTestVisible")) is { } isHitTestVisible)
                control.IsHitTestVisible = isHitTestVisible;
            if (ParseBool(GetProp(element, styles, "ClipToBounds")) is { } clipToBounds)
                control.ClipToBounds = clipToBounds;
            if (ParseInt(GetProp(element, styles, "Panel.ZIndex")) is { } zIndex)
                control.ZIndex = zIndex;
            if (GetProp(element, styles, "Tag") is { } tag)
                control.Tag = tag;
            var tooltip = GetProp(element, styles, "ToolTip.Tip")
                ?? GetProp(element, styles, "ToolTip");
            if (tooltip is not null)
                ToolTip.SetTip(control, ResolveString(tooltip));

            if (control is TextBlock textBlock)
                ApplyTextBlockProperties(textBlock, element, styles);
            if (control is Border border)
                ApplyBorderProperties(border, element, styles);
            if (control is Panel panel)
                ApplyPanelProperties(panel, element, styles);
            if (control is MyImage image)
                ApplyMyImageProperties(image, element, styles);
            if (control is MyCard card
                && ParseCornerRadius(GetProp(element, styles, "CornerRadius")) is { } cardRadius)
                card.CornerRadius = cardRadius;

            ApplyEvent(control, element, styles);
        }

        private void ApplyTextBlockProperties(
            TextBlock block,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseDouble(GetProp(element, styles, "FontSize")) is { } fontSize and > 0d)
                block.FontSize = fontSize;
            if (GetProp(element, styles, "FontWeight") is { } fontWeight)
                block.FontWeight = ParseFontWeight(fontWeight);
            if (GetProp(element, styles, "FontStyle") is { } fontStyle)
                block.FontStyle = ParseFontStyle(fontStyle);
            if (GetProp(element, styles, "FontFamily") is { Length: > 0 } fontFamily)
                block.FontFamily = new FontFamily(fontFamily);
            if (ParseDouble(GetProp(element, styles, "LineHeight")) is { } lineHeight and > 0d)
                block.LineHeight = lineHeight;
            if (ParseEnum<TextWrapping>(GetProp(element, styles, "TextWrapping")) is { } wrapping)
                block.TextWrapping = wrapping;
            if (ParseTextTrimming(GetProp(element, styles, "TextTrimming")) is { } trimming)
                block.TextTrimming = trimming;
            if (ParseEnum<TextAlignment>(GetProp(element, styles, "TextAlignment")) is
                { } alignment)
                block.TextAlignment = alignment;
            if (ParseBrush(GetProp(element, styles, "Foreground")) is { } foreground)
                block.Foreground = foreground;
            if (ParseBrush(GetProp(element, styles, "Background")) is { } background)
                block.Background = background;
        }

        private void ApplyBorderProperties(
            Border border,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseBrush(GetProp(element, styles, "Background")) is { } background)
                border.Background = background;
            if (ParseBrush(GetProp(element, styles, "BorderBrush")) is { } borderBrush)
                border.BorderBrush = borderBrush;
            if (ParseThickness(GetProp(element, styles, "BorderThickness")) is { } borderThickness)
                border.BorderThickness = borderThickness;
            if (ParseThickness(GetProp(element, styles, "Padding")) is { } padding)
                border.Padding = padding;
            if (ParseCornerRadius(GetProp(element, styles, "CornerRadius")) is { } cornerRadius)
                border.CornerRadius = cornerRadius;
        }

        private void ApplyPanelProperties(
            Panel panel,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseBrush(GetProp(element, styles, "Background")) is { } background)
                panel.Background = background;
        }

        private void ApplyMyImageProperties(
            MyImage image,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ResolveString(GetProp(element, styles, "Source")) is { Length: > 0 } source)
                image.Source = NormalizeImageSource(source);
            if (ResolveString(GetProp(element, styles, "FallbackSource")) is
                { Length: > 0 } fallback)
                image.FallbackSource = NormalizeImageSource(fallback);
            if (ParseEnum<Stretch>(GetProp(element, styles, "Stretch")) is { } stretch)
                image.Stretch = stretch;
            if (ParseCornerRadius(GetProp(element, styles, "CornerRadius")) is { } cornerRadius)
                image.CornerRadius = cornerRadius;
        }

        private static bool LooksLikeBitmapPath(string path)
        {
            return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeImageSource(string source)
        {
            const string imagesMarker = "/images/";
            var index = source.IndexOf(imagesMarker, StringComparison.OrdinalIgnoreCase);
            if (source.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
                return index >= 0
                    ? LauncherPaths.ImageRoot + source[(index + imagesMarker.Length)..]
                    : source;
            return source;
        }

        private void ApplyEvent(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var rawType = FindAttribute(element, "EventType")
                ?? FindAttribute(element, "CustomEventService.EventType");
            if (string.IsNullOrWhiteSpace(rawType))
                return;

            if (EventTypeMapper.IsUnsupportedType(rawType))
            {
                result.UnsupportedEventTypes.Add(rawType);
                _rejectedControls.Add(control);
                return;
            }

            if (!EventTypeMapper.TryParse(rawType, out var eventType))
            {
                result.UnrecognizedEventTypes.Add(rawType);
                _rejectedControls.Add(control);
                return;
            }

            var rawData = FindAttribute(element, "EventData")
                ?? FindAttribute(element, "CustomEventService.EventData")
                ?? "";
            CustomEventService.SetEventType(control, eventType);
            CustomEventService.SetEventData(control, ResolveString(rawData) ?? rawData);
        }

        private StackPanel ConvertStackPanel(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var panel = new StackPanel
            {
                Orientation = ParseEnum<Orientation>(GetProp(element, styles, "Orientation"))
                    ?? Orientation.Vertical,
                Spacing = ParseDouble(GetProp(element, styles, "Spacing")) ?? 0d
            };
            ApplyCommon(panel, element, styles);
            AddChildren(panel, element, styleScope);
            return panel;
        }

        private WrapPanel ConvertWrapPanel(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var panel = new WrapPanel
            {
                Orientation = ParseEnum<Orientation>(GetProp(element, styles, "Orientation"))
                    ?? Orientation.Horizontal
            };
            if (ParseDouble(GetProp(element, styles, "ItemWidth")) is { } itemWidth and > 0d)
                panel.ItemWidth = itemWidth;
            if (ParseDouble(GetProp(element, styles, "ItemHeight")) is { } itemHeight and > 0d)
                panel.ItemHeight = itemHeight;
            ApplyCommon(panel, element, styles);
            AddChildren(panel, element, styleScope);
            return panel;
        }

        private DockPanel ConvertDockPanel(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var panel = new DockPanel
            {
                LastChildFill = ParseBool(GetProp(element, styles, "LastChildFill")) ?? true
            };
            ApplyCommon(panel, element, styles);
            AddChildren(panel, element, styleScope);
            return panel;
        }

        private Canvas ConvertCanvas(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var panel = new Canvas();
            ApplyCommon(panel, element, styles);
            AddChildren(panel, element, styleScope);
            return panel;
        }

        private Grid ConvertGrid(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var grid = new Grid();
            ApplyCommon(grid, element, styles);
            if (ParseDouble(GetProp(element, styles, "RowSpacing")) is { } rowSpacing)
                grid.RowSpacing = rowSpacing;
            if (ParseDouble(GetProp(element, styles, "ColumnSpacing")) is { } columnSpacing)
                grid.ColumnSpacing = columnSpacing;

            foreach (var definition in element
                         .Elements()
                         .Where(child => child.Name.LocalName == "Grid.RowDefinitions"))
            foreach (var row in definition
                         .Elements()
                         .Where(child => child.Name.LocalName == "RowDefinition"))
                grid.RowDefinitions.Add(
                    new RowDefinition(ParseGridLength(GetProp(row, styles, "Height"))));
            foreach (var definition in element
                         .Elements()
                         .Where(child => child.Name.LocalName == "Grid.ColumnDefinitions"))
            foreach (var column in definition
                         .Elements()
                         .Where(child => child.Name.LocalName == "ColumnDefinition"))
                grid.ColumnDefinitions.Add(
                    new ColumnDefinition(ParseGridLength(GetProp(column, styles, "Width"))));

            if (grid.RowDefinitions.Count == 0
                && ParseGridDefinitions(GetProp(element, styles, "RowDefinitions")) is
                    { Count: > 0 } rows)
                foreach (var row in rows)
                    grid.RowDefinitions.Add(new RowDefinition(row));
            if (grid.ColumnDefinitions.Count == 0
                && ParseGridDefinitions(GetProp(element, styles, "ColumnDefinitions")) is
                    { Count: > 0 } columns)
                foreach (var column in columns)
                    grid.ColumnDefinitions.Add(new ColumnDefinition(column));

            foreach (var child in element.Elements())
            {
                if (child.Name.LocalName is "Grid.RowDefinitions" or "Grid.ColumnDefinitions")
                    continue;
                if (IsSkippedElement(child))
                    continue;

                var control = ConvertElement(child, styleScope);
                if (control is null)
                    continue;

                if (ParseInt(GetProp(child, styles, "Grid.Row")) is { } rowIndex and >= 0)
                {
                    while (grid.RowDefinitions.Count <= rowIndex)
                        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    Grid.SetRow(control, rowIndex);
                }

                if (ParseInt(GetProp(child, styles, "Grid.Column")) is { } columnIndex and >= 0)
                {
                    while (grid.ColumnDefinitions.Count <= columnIndex)
                        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    Grid.SetColumn(control, columnIndex);
                }

                if (ParseInt(GetProp(child, styles, "Grid.RowSpan")) is { } rowSpan and > 1)
                    Grid.SetRowSpan(control, rowSpan);
                if (ParseInt(GetProp(child, styles, "Grid.ColumnSpan")) is { } columnSpan and > 1)
                    Grid.SetColumnSpan(control, columnSpan);

                ApplyAttachedLayout(control, child, styles);
                grid.Children.Add(control);
            }

            return grid;
        }

        private Border ConvertBorder(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var border = new Border();
            ApplyCommon(border, element, styles);
            border.Child = ConvertSingleChild(element, styleScope);
            return border;
        }

        private Viewbox ConvertViewbox(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var viewbox = new Viewbox();
            ApplyCommon(viewbox, element, styles);
            viewbox.Child = ConvertSingleChild(element, styleScope);
            if (ParseEnum<Stretch>(GetProp(element, styles, "Stretch")) is { } stretch)
                viewbox.Stretch = stretch;
            return viewbox;
        }

        private ScrollViewer ConvertScrollViewer(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var scrollViewer = new ScrollViewer();
            ApplyCommon(scrollViewer, element, styles);
            scrollViewer.Content = ConvertSingleChild(element, styleScope);
            if (GetProp(element, styles, "HorizontalScrollBarVisibility") is { } horizontal
                && ParseEnum<ScrollBarVisibility>(horizontal) is { } horizontalValue)
                scrollViewer.HorizontalScrollBarVisibility = horizontalValue;
            if (GetProp(element, styles, "VerticalScrollBarVisibility") is { } vertical
                && ParseEnum<ScrollBarVisibility>(vertical) is { } verticalValue)
                scrollViewer.VerticalScrollBarVisibility = verticalValue;
            return scrollViewer;
        }

        private MyScrollViewer ConvertMyScrollViewer(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var scrollViewer = new MyScrollViewer();
            ApplyCommon(scrollViewer, element, styles);
            scrollViewer.Content = ConvertSingleChild(element, styleScope);
            if (GetProp(element, styles, "HorizontalScrollBarVisibility") is { } horizontal
                && ParseEnum<ScrollBarVisibility>(horizontal) is { } horizontalValue)
                scrollViewer.HorizontalScrollBarVisibility = horizontalValue;
            if (GetProp(element, styles, "VerticalScrollBarVisibility") is { } vertical
                && ParseEnum<ScrollBarVisibility>(vertical) is { } verticalValue)
                scrollViewer.VerticalScrollBarVisibility = verticalValue;
            return scrollViewer;
        }

        private ContentControl ConvertContentControl(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var contentControl = new ContentControl();
            ApplyCommon(contentControl, element, styles);
            contentControl.Content = ConvertSingleChild(element, styleScope);
            return contentControl;
        }

        private TextBlock ConvertTextBlock(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            };
            ApplyCommon(block, element, styles);
            var text = ResolveString(GetProp(element, styles, "Text"));
            if (!string.IsNullOrEmpty(text))
                block.Text = text;
            else if (element.HasElements)
                AppendInlines(element, block.Inlines!, styles);
            else if (!string.IsNullOrWhiteSpace(element.Value))
                block.Text = element.Value.Trim();
            return block;
        }

        private TextBlock ConvertInlineTextBlock(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            };
            ApplyCommon(block, element, styles);
            var text = ResolveString(GetProp(element, styles, "Text"));
            if (!string.IsNullOrEmpty(text))
                block.Text = text;
            else
                AppendInlines(element, block.Inlines!, styles);
            return block;
        }

        private void AppendInlines(
            XElement element,
            InlineCollection inlines,
            IReadOnlyDictionary<string, string> styles)
        {
            foreach (var node in element.Nodes())
                switch (node)
                {
                    case XText text when !string.IsNullOrWhiteSpace(text.Value):
                        inlines.Add(new Run(text.Value));
                        break;
                    case XElement { Name.LocalName: "LineBreak" }:
                        inlines.Add(new Run("\n"));
                        break;
                    case XElement { Name.LocalName: "Run" } runElement:
                    {
                        var run = new Run(
                            ResolveString(FindAttribute(runElement, "Text")) ?? runElement.Value);
                        if (ParseBrush(FindAttribute(runElement, "Foreground")) is { } brush)
                            run.Foreground = brush;
                        inlines.Add(run);
                        break;
                    }
                    case XElement child when child.Name.LocalName.Contains('.'):
                        if (child.Name.LocalName.EndsWith(
                                ".Inlines",
                                StringComparison.OrdinalIgnoreCase))
                            AppendInlines(child, inlines, styles);
                        break;
                    case XElement child:
                        var inner = new TextBlock();
                        AppendInlines(child, inner.Inlines!, styles);
                        foreach (var inline in inner.Inlines!.ToArray())
                            inlines.Add(inline);
                        break;
                }
        }

        private MyImage ConvertImage(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var image = new MyImage();
            ApplyCommon(image, element, styles);
            ApplyMyImageProperties(image, element, styles);
            return image;
        }

        private Border ConvertShape(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var border = new Border
            {
                Background = ParseBrush(GetProp(element, styles, "Fill"))
                    ?? ParseBrush(GetProp(element, styles, "Stroke"))
                    ?? Brushes.Transparent
            };
            ApplyCommon(border, element, styles);
            if (double.IsNaN(border.Height))
                border.Height = 2d;
            if (element.Name.LocalName == "Ellipse"
                && !double.IsNaN(border.Width)
                && !double.IsNaN(border.Height))
                border.CornerRadius = new CornerRadius(Math.Max(border.Width, border.Height) / 2d);
            return border;
        }

        private Border ConvertPath(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var border = new Border();
            ApplyCommon(border, element, styles);
            return border;
        }

        private MyButton ConvertMyButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyButton
            {
                Text = ResolveString(GetProp(element, styles, "Text"))
                    ?? ResolveString(GetProp(element, styles, "Content"))
                    ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseEnum<MyButton.ColorState>(GetProp(element, styles, "ColorType"))
                is { } colorType)
                button.ColorType = colorType;
            if (ParseThickness(GetProp(element, styles, "Padding")) is { } padding)
                button.Padding = padding;
            return button;
        }

        private MyTextButton ConvertMyTextButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyTextButton
            {
                Text = ResolveString(GetProp(element, styles, "Text"))
                    ?? ResolveString(GetProp(element, styles, "Content"))
                    ?? ""
            };
            ApplyCommon(button, element, styles);
            return button;
        }

        private MyIconButton ConvertMyIconButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyIconButton
            {
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = GetProp(element, styles, "Logo") ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                button.LogoScale = logoScale;
            return button;
        }

        private MyIconTextButton ConvertMyIconTextButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyIconTextButton
            {
                Text = ResolveString(GetProp(element, styles, "Text"))
                    ?? ResolveString(GetProp(element, styles, "Content"))
                    ?? "",
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = GetProp(element, styles, "Logo") ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseEnum<MyIconTextButton.ColorState>(GetProp(element, styles, "ColorType"))
                is { } colorType)
                button.ColorType = colorType;
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                button.LogoScale = logoScale;
            return button;
        }

        private MyExtraButton ConvertMyExtraButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyExtraButton
            {
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = GetProp(element, styles, "Logo") ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                button.LogoScale = logoScale;
            return button;
        }

        private MyExtraTextButton ConvertMyExtraTextButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyExtraTextButton
            {
                Text = ResolveString(GetProp(element, styles, "Text"))
                    ?? ResolveString(GetProp(element, styles, "Content"))
                    ?? "",
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = GetProp(element, styles, "Logo") ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                button.LogoScale = logoScale;
            return button;
        }

        private MyCollapseBar ConvertMyCollapseBar(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var bar = new MyCollapseBar
            {
                Title = ResolveString(GetProp(element, styles, "Title")) ?? ""
            };
            ApplyCommon(bar, element, styles);
            AddChildren(bar, element, styleScope);
            return bar;
        }

        private MyCard ConvertMyCard(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var card = new MyCard();
            ApplyCommon(card, element, styles);

            var titleElement = element
                .Elements()
                .FirstOrDefault(child => child.Name.LocalName == "MyCard.Title");
            var title = titleElement is not null
                ? titleElement.Value.Trim()
                : ResolveString(GetProp(element, styles, "Title"));
            if (!string.IsNullOrEmpty(title))
                card.Title = title;

            var canSwap = ParseBool(GetProp(element, styles, "CanSwap")) ?? false;
            var isSwapped = ParseBool(GetProp(element, styles, "IsSwapped"))
                ?? ParseBool(GetProp(element, styles, "IsSwaped"))
                ?? false;

            var childCountBefore = card.Children.Count;
            AddChildren(card, element, styleScope);

            if (canSwap || isSwapped)
            {
                card.CanSwap = true;
                if (card.Children.Count > childCountBefore)
                    card.SwapControl = card.Children[childCountBefore];
                if (isSwapped)
                    card.IsSwapped = true;
            }

            return card;
        }

        private MyHint ConvertMyHint(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var hint = new MyHint
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(hint, element, styles);
            if (ParseBool(GetProp(element, styles, "IsWarn")) is { } isWarn)
                hint.IsWarn = isWarn;
            if (string.IsNullOrWhiteSpace(hint.Text) && !element.HasElements)
                hint.Text = element.Value.Trim();
            return hint;
        }

        private MyListItem ConvertMyListItem(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var logo = GetProp(element, styles, "Logo") ?? "";
            if (LooksLikeBitmapPath(logo) && !XamlSecurityValidator.IsSafeSource(logo))
                logo = "";
            if (logo.Contains("/images/", StringComparison.OrdinalIgnoreCase)
                || logo.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                logo = NormalizeImageSource(logo);
            var item = new MyListItem
            {
                Title = ResolveString(GetProp(element, styles, "Title")) ?? "",
                Info = ResolveString(GetProp(element, styles, "Info")) ?? "",
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = logo
            };
            ApplyCommon(item, element, styles);
            if (ParseEnum<MyListItem.CheckType>(GetProp(element, styles, "Type"))
                is { } checkType)
                item.Type = checkType;
            if (ParseBool(GetProp(element, styles, "Checked")) is { } isChecked)
                item.Checked = isChecked;
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                item.LogoScale = logoScale;
            return item;
        }

        private MyLoading ConvertMyLoading(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var loading = new MyLoading
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(loading, element, styles);
            return loading;
        }

        private MyTextBox ConvertMyTextBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var textBox = new MyTextBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? "",
                HintText = ResolveString(GetProp(element, styles, "HintText"))
                    ?? ResolveString(GetProp(element, styles, "Watermark"))
                    ?? ""
            };
            ApplyCommon(textBox, element, styles);
            if (ParseInt(GetProp(element, styles, "MaxLength")) is { } maxLength and > 0)
                textBox.MaxLength = maxLength;
            if (ParseBool(GetProp(element, styles, "IsReadOnly")) is { } isReadOnly)
                textBox.IsReadOnly = isReadOnly;
            return textBox;
        }

        private MyPasswordBox ConvertMyPasswordBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var textBox = new MyPasswordBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? "",
                HintText = ResolveString(GetProp(element, styles, "HintText"))
                    ?? ResolveString(GetProp(element, styles, "Watermark"))
                    ?? ""
            };
            ApplyCommon(textBox, element, styles);
            if (ParseInt(GetProp(element, styles, "MaxLength")) is { } maxLength and > 0)
                textBox.MaxLength = maxLength;
            return textBox;
        }

        private MyComboBoxItem ConvertMyComboBoxItem(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var item = new MyComboBoxItem();
            var content = ResolveString(GetProp(element, styles, "Content"))
                ?? ResolveString(GetProp(element, styles, "Text"))
                ?? (element.HasElements ? null : element.Value.Trim());
            if (!string.IsNullOrEmpty(content))
                item.Content = content;
            else if (element.HasElements)
                item.Content = ConvertSingleChild(element, styleScope);
            ApplyCommon(item, element, styles);
            return item;
        }

        private MyComboBox ConvertMyComboBox(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var comboBox = new MyComboBox
            {
                HintText = ResolveString(GetProp(element, styles, "HintText"))
                    ?? ResolveString(GetProp(element, styles, "Watermark"))
                    ?? ""
            };
            ApplyCommon(comboBox, element, styles);
            if (ParseInt(GetProp(element, styles, "SelectedIndex")) is { } selectedIndex)
                comboBox.SelectedIndex = selectedIndex;

            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;
                if (child.Name.LocalName is not ("MyComboBoxItem" or "ComboBoxItem"))
                    continue;
                if (ConvertElement(child, styleScope) is { } item)
                    comboBox.Items.Add(item);
            }

            return comboBox;
        }

        private MyCheckBox ConvertMyCheckBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var checkBox = new MyCheckBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(checkBox, element, styles);
            var isChecked = ParseBool(GetProp(element, styles, "IsChecked"))
                ?? ParseBool(GetProp(element, styles, "Checked"));
            if (isChecked is { } checkedValue)
                checkBox.Checked = checkedValue;
            return checkBox;
        }

        private MyRadioBox ConvertMyRadioBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var radioBox = new MyRadioBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(radioBox, element, styles);
            if (ParseBool(GetProp(element, styles, "Checked")) is { } isChecked)
                radioBox.Checked = isChecked;
            return radioBox;
        }

        private MyRadioButton ConvertMyRadioButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var radioButton = new MyRadioButton
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(radioButton, element, styles);
            return radioButton;
        }

        private MySlider ConvertMySlider(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var slider = new MySlider();
            ApplyCommon(slider, element, styles);
            if (ParseInt(GetProp(element, styles, "Value")) is { } value)
                slider.Value = value;
            return slider;
        }

        private MySearchBox ConvertMySearchBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var searchBox = new MySearchBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? "",
                HintText = ResolveString(GetProp(element, styles, "HintText"))
                    ?? ResolveString(GetProp(element, styles, "Watermark"))
                    ?? ""
            };
            ApplyCommon(searchBox, element, styles);
            return searchBox;
        }

        private Border ConvertSeparator(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var separator = new Border
            {
                Height = 1d,
                Background = ParseBrush(GetProp(element, styles, "Background"))
                    ?? Brushes.Gray
            };
            ApplyCommon(separator, element, styles);
            return separator;
        }

        private Control? ConvertSingleChild(XElement element, StyleScope styleScope)
        {
            var children = new List<Control>();
            foreach (var child in element.Elements())
            {
                if (child.Name.LocalName is var name
                    && (name.EndsWith(".Child", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".Content", StringComparison.OrdinalIgnoreCase)))
                {
                    children.AddRange(
                        (
                            from inner in child.Elements()
                            where !IsSkippedElement(inner)
                            select ConvertElement(inner, styleScope)).OfType<Control>());

                    continue;
                }

                if (IsSkippedElement(child))
                    continue;
                var control = ConvertElement(child, styleScope);
                if (control is not null)
                    children.Add(control);
            }

            if (children.Count == 0)
                return null;
            if (children.Count == 1)
                return children[0];

            var panel = new StackPanel();
            foreach (var child in children)
                panel.Children.Add(child);
            return panel;
        }

        private void AddChildren(Panel panel, XElement element, StyleScope styleScope)
        {
            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;
                var control = ConvertElement(child, styleScope);
                if (control is null)
                    continue;
                ApplyAttachedLayout(
                    control,
                    child,
                    styleScope.Resolve(child.Name.LocalName, child));
                panel.Children.Add(control);
            }
        }

        private void ApplyAttachedLayout(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseInt(GetProp(element, styles, "Grid.Row")) is { } row and >= 0)
                Grid.SetRow(control, row);
            if (ParseInt(GetProp(element, styles, "Grid.Column")) is { } column and >= 0)
                Grid.SetColumn(control, column);
            if (ParseInt(GetProp(element, styles, "Grid.RowSpan")) is { } rowSpan and > 1)
                Grid.SetRowSpan(control, rowSpan);
            if (ParseInt(GetProp(element, styles, "Grid.ColumnSpan")) is { } columnSpan and > 1)
                Grid.SetColumnSpan(control, columnSpan);
            if (ParseEnum<Dock>(GetProp(element, styles, "DockPanel.Dock")) is { } dock)
                DockPanel.SetDock(control, dock);
            if (ParseDouble(GetProp(element, styles, "Canvas.Left")) is { } left)
                Canvas.SetLeft(control, left);
            if (ParseDouble(GetProp(element, styles, "Canvas.Top")) is { } top)
                Canvas.SetTop(control, top);
        }

        private static bool IsSkippedElement(XElement element)
        {
            var name = element.Name.LocalName;
            return name.Contains('.') || IgnoredElementNames.Contains(name);
        }

        private static void CollectStyles(XElement element, StyleScope scope)
        {
            foreach (var resources in element
                         .Elements()
                         .Where(child =>
                             child.Name.LocalName.EndsWith(
                                 ".Resources",
                                 StringComparison.OrdinalIgnoreCase)
                             || child.Name.LocalName.Equals(
                                 "Resources",
                                 StringComparison.OrdinalIgnoreCase)))
            foreach (var style in EnumerateStyles(resources))
            {
                var setters = ReadSetters(style);
                if (setters.Count == 0)
                    continue;

                var key = FindAttribute(style, "Key");
                if (!string.IsNullOrWhiteSpace(key))
                    scope.ByKey[key] = setters;

                var targetType = FindAttribute(style, "TargetType");
                if (!string.IsNullOrWhiteSpace(targetType))
                {
                    targetType = StripTypePrefix(targetType);
                    if (scope.ByType.TryGetValue(targetType, out var existing))
                        foreach (var pair in setters)
                            existing[pair.Key] = pair.Value;
                    else
                        scope.ByType[targetType] = new Dictionary<string, string>(
                            setters,
                            StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        private static IEnumerable<XElement> EnumerateStyles(XElement container)
        {
            foreach (var child in container.Elements())
                switch (child.Name.LocalName)
                {
                    case "Style":
                        yield return child;
                        continue;

                    case "ResourceDictionary":
                    {
                        foreach (var nested in EnumerateStyles(child))
                            yield return nested;
                        break;
                    }
                }
        }

        private static Dictionary<string, string> ReadSetters(XElement style)
        {
            var setters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var setterElements =
                style.Elements().Where(child => child.Name.LocalName == "Setter").ToList();
            foreach (var setterContainer in style
                         .Elements()
                         .Where(child => child.Name.LocalName == "Style.Setters"))
                setterElements.AddRange(
                    setterContainer
                        .Elements()
                        .Where(child => child.Name.LocalName == "Setter"));

            foreach (var setter in setterElements)
            {
                var property = FindAttribute(setter, "Property");
                var value = FindAttribute(setter, "Value")
                    ?? setter
                        .Elements()
                        .FirstOrDefault(child => child.Name.LocalName == "Setter.Value")
                        ?.Value;
                if (string.IsNullOrWhiteSpace(property) || value is null)
                    continue;
                if (property.Contains("Event", StringComparison.OrdinalIgnoreCase)
                    || property.Contains("Command", StringComparison.OrdinalIgnoreCase))
                    continue;
                setters[property] = value;
            }

            return setters;
        }

        private static string StripTypePrefix(string value)
        {
            var trimmed = value.Trim();
            var match = Regex.Match(trimmed, @"^\{x:Type\s+([^}]+)\}$");
            if (match.Success)
                trimmed = match.Groups[1].Value.Trim();
            var separator = trimmed.LastIndexOf(':');
            return separator >= 0 ? trimmed[(separator + 1)..] : trimmed;
        }

        private string? ResolveString(string? raw)
        {
            if (raw is null)
                return null;

            var match = MarkupExtensionPattern.Match(raw.Trim());
            if (match.Success)
            {
                var extension = match.Groups[1].Value;
                if (extension is "DynamicResource" or "StaticResource")
                {
                    var key = match.Groups[2].Value.Trim();
                    return resolveResource(key)?.ToString() ?? "";
                }
            }

            return raw;
        }

        private IBrush? ParseBrush(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var resolved = ResolveString(raw);
            if (resolved is not null && !ReferenceEquals(resolved, raw))
                if (resolveResource(MarkupKey(raw) ?? "") is IBrush brush)
                    return brush;

            var value = (resolved ?? raw).Trim();
            if (value.StartsWith('#'))
                try
                {
                    return new SolidColorBrush(Color.Parse(value));
                }
                catch (Exception)
                {
                    return null;
                }

            return value switch
            {
                "Transparent" => Brushes.Transparent,
                "White" => Brushes.White,
                "Black" => Brushes.Black,
                "Red" => Brushes.Red,
                "Blue" => Brushes.Blue,
                "Green" => Brushes.Green,
                "Gray" or "Grey" => Brushes.Gray,
                _ => null
            };
        }

        private static string? MarkupKey(string raw)
        {
            var match = MarkupExtensionPattern.Match(raw.Trim());
            return match.Success ? match.Groups[2].Value.Trim() : null;
        }
    }

    private sealed class StyleScope(StyleScope? parent)
    {
        public Dictionary<string, Dictionary<string, string>> ByKey { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Dictionary<string, string>> ByType { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        private StyleScope? Parent { get; } = parent;

        public IReadOnlyDictionary<string, string> Resolve(string elementName, XElement element)
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var chain = new Stack<StyleScope>();
            for (var scope = this; scope is not null; scope = scope.Parent)
                chain.Push(scope);

            foreach (var scope in chain)
                if (scope.ByType.TryGetValue(elementName, out var setters))
                    foreach (var pair in setters)
                        merged[pair.Key] = pair.Value;

            var styleKey = MarkupKey(FindAttribute(element, "Style"));
            if (!string.IsNullOrWhiteSpace(styleKey))
                for (var scope = this; scope is not null; scope = scope.Parent)
                    if (scope.ByKey.TryGetValue(styleKey, out var setters))
                    {
                        foreach (var pair in setters)
                            merged[pair.Key] = pair.Value;
                        break;
                    }

            return merged;
        }

        private static string? MarkupKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            var match = Regex.Match(raw.Trim(), @"^\{[A-Za-z_][\w.:]*\s+([^{}]*)\}$");
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }
}