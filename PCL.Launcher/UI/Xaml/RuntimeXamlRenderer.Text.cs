using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private TextBlock ConvertTextBlock(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.NoWrap
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
                TextWrapping = TextWrapping.NoWrap
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
                    case XElement { Name.LocalName: "Bold" } boldElement:
                    {
                        var bold = new Bold();
                        AppendInlines(boldElement, bold.Inlines!, styles);
                        inlines.Add(bold);
                        break;
                    }
                    case XElement { Name.LocalName: "Italic" } italicElement:
                    {
                        var italic = new Italic();
                        AppendInlines(italicElement, italic.Inlines!, styles);
                        inlines.Add(italic);
                        break;
                    }
                    case XElement { Name.LocalName: "Underline" } underlineElement:
                    {
                        var span = new Span
                        {
                            TextDecorations = TextDecorations.Underline
                        };
                        AppendInlines(underlineElement, span.Inlines!, styles);
                        inlines.Add(span);
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
    }
}