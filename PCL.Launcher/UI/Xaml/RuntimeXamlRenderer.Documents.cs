using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.UI.Controls;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyStyles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Control ConvertFlowDocumentScrollViewer(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            ApplyCommon(panel, element, styles);

            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;

                if (child.Name.LocalName.Equals("FlowDocument", StringComparison.OrdinalIgnoreCase))
                {
                    panel.Children.Add(
                        ConvertFlowDocument(
                            child,
                            styleScope,
                            styleScope.Resolve("FlowDocument", child)));
                    continue;
                }

                var control = ConvertElement(child, styleScope);
                if (control is not null)
                    panel.Children.Add(control);
            }

            return panel;
        }

        private Control ConvertFlowDocument(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            ApplyCommon(panel, element, styles);

            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;
                panel.Children.Add(ConvertDocumentBlock(child, styleScope, styles));
            }

            return panel;
        }

        private Control ConvertDocumentBlock(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> inheritedStyles)
        {
            return element.Name.LocalName.ToLowerInvariant() switch
            {
                "paragraph" => ConvertParagraph(
                    element,
                    styleScope,
                    styleScope.Resolve("Paragraph", element),
                    inheritedStyles),
                "list" => ConvertDocumentList(
                    element,
                    styleScope,
                    styleScope.Resolve("List", element),
                    inheritedStyles),
                "section" => ConvertDocumentSection(element, styleScope, inheritedStyles),
                _ => ConvertElement(element, styleScope) ?? new Border()
            };
        }

        private Control ConvertDocumentSection(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> inheritedStyles)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            var styles = styleScope.Resolve("Section", element);
            ApplyCommon(panel, element, MergeStyles(inheritedStyles, styles));
            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;
                panel.Children.Add(
                    ConvertDocumentBlock(child, styleScope, MergeStyles(inheritedStyles, styles)));
            }

            return panel;
        }

        private Control ConvertWpfTextBox(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var text = element.HasElements ? "" : element.Value.Trim();
            var styleKey = MarkupKey(FindAttribute(element, "Style"));
            var template = styleScope.ResolveTemplate(styleKey, "TextBox");
            if (template is not null
                && InstantiateTextBoxTemplate(template, text, styles, styleScope) is
                    { } templated)
                return templated;

            var textBox = new MyTextBox
            {
                Text = text,
                IsReadOnly = true
            };
            ApplyCommon(textBox, element, styles);
            if (ParseInt(GetProp(element, styles, "MaxLength")) is { } maxLength and > 0)
                textBox.MaxLength = maxLength;
            return textBox;
        }

        private TextBlock ConvertParagraph(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles,
            IReadOnlyDictionary<string, string>? inheritedStyles = null)
        {
            var merged = MergeStyles(inheritedStyles ?? EmptyStyles, styles);
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            };
            ApplyCommon(block, element, merged);
            if (!double.IsNaN(block.LineHeight)
                && block.LineHeight < block.FontSize * 1.1d)
                block.LineHeight = double.NaN;
            AppendDocumentInlines(element, block.Inlines!, styleScope, merged);
            return block;
        }

        private Control ConvertDocumentList(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles,
            IReadOnlyDictionary<string, string>? inheritedStyles = null)
        {
            var merged = MergeStyles(inheritedStyles ?? EmptyStyles, styles);
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            ApplyCommon(panel, element, merged);

            var markerStyle = ParseInt(GetProp(element, merged, "MarkerStyle")) ?? 0;
            var index = 0;
            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;

                if (child.Name.LocalName.Equals("ListItem", StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    panel.Children.Add(
                        ConvertDocumentListItem(
                            child,
                            styleScope,
                            merged,
                            FormatListMarker(markerStyle, index)));
                }
                else
                {
                    panel.Children.Add(ConvertDocumentBlock(child, styleScope, merged));
                }
            }

            return panel;
        }

        private Control ConvertDocumentListItem(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> inheritedStyles,
            string marker = "• ")
        {
            var merged = MergeStyles(
                inheritedStyles,
                styleScope.Resolve("ListItem", element));
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*")
            };
            ApplyCommon(grid, element, merged);

            var bullet = new TextBlock
            {
                Text = marker,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            ApplyDocumentFont(bullet, merged);
            Grid.SetColumn(bullet, 0);
            grid.Children.Add(bullet);

            var content = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            Grid.SetColumn(content, 1);
            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;
                content.Children.Add(ConvertDocumentBlock(child, styleScope, merged));
            }

            if (content.Children.Count == 0 && !element.HasElements)
                content.Children.Add(
                    new TextBlock
                    {
                        Text = element.Value.Trim(),
                        TextWrapping = TextWrapping.Wrap
                    });
            grid.Children.Add(content);
            return grid;
        }

        private void AppendDocumentInlines(
            XElement element,
            InlineCollection inlines,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            foreach (var node in element.Nodes())
                switch (node)
                {
                    case XText text when !string.IsNullOrWhiteSpace(text.Value):
                        inlines.Add(new Run(text.Value));
                        break;
                    case XElement child:
                        AppendDocumentInline(child, inlines, styleScope, styles);
                        break;
                }
        }

        private void AppendDocumentInline(
            XElement element,
            InlineCollection inlines,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            switch (element.Name.LocalName.ToLowerInvariant())
            {
                case "linebreak":
                    inlines.Add(new LineBreak());
                    return;
                case "run":
                    inlines.Add(
                        new Run(ResolveString(FindAttribute(element, "Text")) ?? element.Value));
                    return;
                case "span":
                {
                    var span = new Span();
                    AppendDocumentInlines(element, span.Inlines!, styleScope, styles);
                    inlines.Add(span);
                    return;
                }
                case "bold":
                {
                    var bold = new Bold();
                    AppendDocumentInlines(element, bold.Inlines!, styleScope, styles);
                    inlines.Add(bold);
                    return;
                }
                case "italic":
                {
                    var italic = new Italic();
                    AppendDocumentInlines(element, italic.Inlines!, styleScope, styles);
                    inlines.Add(italic);
                    return;
                }
                case "underline":
                {
                    var span = new Span
                    {
                        TextDecorations = TextDecorations.Underline
                    };
                    AppendDocumentInlines(element, span.Inlines!, styleScope, styles);
                    inlines.Add(span);
                    return;
                }
                case "textbox":
                    inlines.Add(
                        new InlineUIContainer
                        {
                            Child = CreateInlineCode(element, styleScope)
                        });
                    return;
                default:
                {
                    var control = ConvertElement(element, styleScope);
                    if (control is not null)
                        inlines.Add(
                            new InlineUIContainer
                            {
                                Child = control
                            });
                    else if (!string.IsNullOrWhiteSpace(element.Value))
                        inlines.Add(new Run(element.Value));
                    return;
                }
            }
        }

        private Control CreateInlineCode(XElement element, StyleScope styleScope)
        {
            var styles = styleScope.Resolve("TextBox", element);
            var styleKey = MarkupKey(FindAttribute(element, "Style"));
            var template = styleScope.ResolveTemplate(styleKey, "TextBox");
            if (template is not null
                && InstantiateTextBoxTemplate(
                    template,
                    element.Value.Trim(),
                    styles,
                    styleScope) is { } templated)
                return templated;

            var border = new Border
            {
                Background = ParseBrush(GetProp(element, styles, "Background"))
                    ?? ParseBrush("{DynamicResource ColorBrush6}"),
                CornerRadius = ParseCornerRadius(GetProp(element, styles, "CornerRadius"))
                    ?? new CornerRadius(4),
                Padding = ParseThickness(GetProp(element, styles, "Padding"))
                    ?? new Thickness(4, 0, 4, 0),
                Child = new TextBlock
                {
                    Text = element.Value.Trim(),
                    FontFamily = ParseFontFamily(GetProp(element, styles, "FontFamily")),
                    FontSize = ParseDouble(GetProp(element, styles, "FontSize")) ?? 13d,
                    Foreground = ParseBrush(GetProp(element, styles, "Foreground"))
                        ?? ParseBrush("{DynamicResource ColorBrush1}")
                }
            };
            if (ParseDouble(GetProp(element, styles, "Height")) is { } height and > 0d)
                border.Height = height;
            return border;
        }

        private static string FormatListMarker(int markerStyle, int index)
        {
            return markerStyle switch
            {
                1 => "• ",
                2 => "○ ",
                3 => "▪ ",
                4 => "□ ",
                5 => $"{ToRoman(index).ToLowerInvariant()}. ",
                6 => $"{ToRoman(index)}. ",
                7 => $"{ToAlpha(index).ToLowerInvariant()}. ",
                8 => $"{ToAlpha(index)}. ",
                9 => $"{index}. ",
                _ => ""
            };
        }

        private static string ToAlpha(int value)
        {
            if (value <= 0)
                return "";
            var result = "";
            while (value > 0)
            {
                value--;
                result = (char)('A' + value % 26) + result;
                value /= 26;
            }

            return result;
        }

        private static string ToRoman(int value)
        {
            if (value <= 0)
                return "";
            var values = new[]
            {
                (1000, "M"),
                (900, "CM"),
                (500, "D"),
                (400, "CD"),
                (100, "C"),
                (90, "XC"),
                (50, "L"),
                (40, "XL"),
                (10, "X"),
                (9, "IX"),
                (5, "V"),
                (4, "IV"),
                (1, "I")
            };
            var result = "";
            foreach (var (number, symbol) in values)
                while (value >= number)
                {
                    result += symbol;
                    value -= number;
                }

            return result;
        }

        private static FontFamily ParseFontFamily(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? FontFamily.Default : new FontFamily(value);
        }

        private void ApplyDocumentFont(TextBlock block, IReadOnlyDictionary<string, string> styles)
        {
            if (ParseDouble(styles.GetValueOrDefault("FontSize")) is { } fontSize and > 0d)
                block.FontSize = fontSize;
            if (ParseBrush(styles.GetValueOrDefault("Foreground")) is { } foreground)
                block.Foreground = foreground;
            if (styles.GetValueOrDefault("FontFamily") is { Length: > 0 } fontFamily)
                block.FontFamily = new FontFamily(fontFamily);
        }

        private static IReadOnlyDictionary<string, string> MergeStyles(
            IReadOnlyDictionary<string, string> baseStyles,
            IReadOnlyDictionary<string, string>? overrides)
        {
            if (overrides is null || overrides.Count == 0)
                return baseStyles;

            var merged = new Dictionary<string, string>(
                baseStyles,
                StringComparer.OrdinalIgnoreCase);
            foreach (var pair in overrides)
                merged[pair.Key] = pair.Value;
            return merged;
        }
    }
}