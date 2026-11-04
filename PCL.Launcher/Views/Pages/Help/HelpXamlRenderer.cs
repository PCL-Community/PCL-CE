using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.UI.Controls;

namespace PCL.Views.Pages;

internal static class HelpXamlRenderer
{
    private const string PresentationNamespace =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly Regex DynamicResourcePattern =
        new(@"\{DynamicResource\s+([^}]+)\}", RegexOptions.Compiled);

    public static Control? Render(string fragment, Func<string, IBrush?> resolveBrush)
    {
        if (string.IsNullOrWhiteSpace(fragment))
            return null;

        try
        {
            var wrapped =
                "<Root xmlns=\""
                + PresentationNamespace
                + "\""
                + " xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\""
                + " xmlns:local=\"clr-namespace:PCL\">"
                + fragment
                + "</Root>";
            var document = XDocument.Parse(wrapped);
            var root = document.Root;
            if (root is null)
                return null;

            var panel = new StackPanel
            {
                Spacing = 12d
            };
            foreach (var element in root.Elements())
                if (ConvertElement(element, resolveBrush) is { } control)
                    panel.Children.Add(control);

            return panel.Children.Count > 0 ? panel : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Control? ConvertElement(XElement element, Func<string, IBrush?> resolveBrush)
    {
        var name = element.Name.LocalName;
        switch (name)
        {
            case "TextBlock":
                return ConvertTextBlock(element, resolveBrush);
            case "StackPanel":
                return ConvertStackPanel(element, resolveBrush);
            case "Grid":
                return ConvertGrid(element, resolveBrush);
            case "WrapPanel":
                return ConvertWrapPanel(element, resolveBrush);
            case "MyCard":
            case "Border":
                return ConvertCard(element, resolveBrush);
            case "Image":
                return ConvertImage(element);
            case "Rectangle":
                return ConvertRectangle(element, resolveBrush);
            case "Button":
                return ConvertButton(element, resolveBrush);
            case "Run":
            case "Span":
            case "Bold":
            case "Italic":
            {
                var text = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap
                };
                AppendInlines(element, text.Inlines!, resolveBrush);
                return text;
            }
            default:
            {
                var text = element.Value.Trim();
                return string.IsNullOrEmpty(text)
                    ? null
                    : new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap
                    };
            }
        }
    }

    private static TextBlock ConvertTextBlock(XElement element, Func<string, IBrush?> resolveBrush)
    {
        var block = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };
        var text = element.Attribute("Text")?.Value;
        if (!string.IsNullOrEmpty(text))
            block.Text = text;
        else if (element.HasElements)
            AppendInlines(element, block.Inlines!, resolveBrush);

        ApplyCommon(element, block, resolveBrush);
        if (block.FontSize <= 0d)
            block.FontSize = 13d;
        return block;
    }

    private static void AppendInlines(
        XElement element,
        InlineCollection inlines,
        Func<string, IBrush?> resolveBrush)
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
                case XElement { Name.LocalName: "Run" } child:
                {
                    var run = new Run(child.Attribute("Text")?.Value ?? child.Value);
                    if (ParseBrush(child.Attribute("Foreground")?.Value, resolveBrush) is { } brush)
                        run.Foreground = brush;
                    inlines.Add(run);
                    break;
                }
                case XElement child:
                {
                    var inner = new TextBlock();
                    AppendInlines(child, inner.Inlines!, resolveBrush);
                    foreach (var inline in inner.Inlines!.ToArray())
                        inlines.Add(inline);
                    break;
                }
            }
    }

    private static Control ConvertStackPanel(XElement element, Func<string, IBrush?> resolveBrush)
    {
        var panel = new StackPanel
        {
            Orientation = string.Equals(
                element.Attribute("Orientation")?.Value,
                "Horizontal",
                StringComparison.OrdinalIgnoreCase)
                ? Orientation.Horizontal
                : Orientation.Vertical,
            Spacing = ParseDouble(element.Attribute("Spacing")?.Value) ?? 0d
        };
        ApplyCommon(element, panel, resolveBrush);
        foreach (var child in element.Elements())
            if (ConvertElement(child, resolveBrush) is { } control)
                panel.Children.Add(control);
        return panel;
    }

    private static Control ConvertWrapPanel(XElement element, Func<string, IBrush?> resolveBrush)
    {
        var panel = new WrapPanel();
        ApplyCommon(element, panel, resolveBrush);
        foreach (var child in element.Elements())
            if (ConvertElement(child, resolveBrush) is { } control)
                panel.Children.Add(control);
        return panel;
    }

    private static Control ConvertGrid(XElement element, Func<string, IBrush?> resolveBrush)
    {
        var grid = new Grid();
        ApplyCommon(element, grid, resolveBrush);

        foreach (var definition in element
                     .Elements()
                     .Where(child => child.Name.LocalName == "Grid.RowDefinitions"))
        foreach (var row in definition.Elements())
            grid.RowDefinitions.Add(
                new RowDefinition(ParseGridLength(row.Attribute("Height")?.Value)));
        foreach (var definition in element
                     .Elements()
                     .Where(child => child.Name.LocalName == "Grid.ColumnDefinitions"))
        foreach (var column in definition.Elements())
            grid.ColumnDefinitions.Add(
                new ColumnDefinition(ParseGridLength(column.Attribute("Width")?.Value)));

        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName is "Grid.RowDefinitions" or "Grid.ColumnDefinitions")
                continue;
            if (ConvertElement(child, resolveBrush) is not { } control)
                continue;

            if (int.TryParse(child.Attribute("Grid.Row")?.Value, out var rowIndex)
                && rowIndex >= 0)
            {
                while (grid.RowDefinitions.Count <= rowIndex)
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(control, rowIndex);
            }

            if (int.TryParse(child.Attribute("Grid.Column")?.Value, out var columnIndex)
                && columnIndex >= 0)
            {
                while (grid.ColumnDefinitions.Count <= columnIndex)
                    grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                Grid.SetColumn(control, columnIndex);
            }

            grid.Children.Add(control);
        }

        if (grid.RowDefinitions.Count == 0 && grid.Children.Count > 1)
            foreach (var child in grid.Children.Cast<Control>())
                Grid.SetRow(child, grid.Children.IndexOf(child));

        return grid;
    }

    private static Control ConvertCard(XElement element, Func<string, IBrush?> resolveBrush)
    {
        var border = new Border
        {
            Padding = new Thickness(18d, 14d, 18d, 14d)
        };
        border.Classes.Add("Card");
        ApplyCommon(element, border, resolveBrush);

        var content = new StackPanel
        {
            Spacing = 8d
        };
        foreach (var child in element.Elements())
            if (ConvertElement(child, resolveBrush) is { } control)
                content.Children.Add(control);
        border.Child = content;
        return border;
    }

    private static Control? ConvertImage(XElement element)
    {
        var source = element.Attribute("Source")?.Value;
        if (string.IsNullOrWhiteSpace(source))
            return null;

        const string imagesMarker = "/images/";
        var index = source.IndexOf(imagesMarker, StringComparison.OrdinalIgnoreCase);
        if (source.StartsWith("pack://", StringComparison.OrdinalIgnoreCase) && index >= 0)
            source = LauncherPaths.ImageRoot + source[(index + imagesMarker.Length)..];

        var image = new MyImage
        {
            Source = source,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        if (ParseDouble(element.Attribute("Width")?.Value) is { } width and > 0d)
            image.Width = width;
        if (ParseDouble(element.Attribute("Height")?.Value) is { } height and > 0d)
            image.Height = height;
        return image;
    }

    private static Control? ConvertRectangle(XElement element, Func<string, IBrush?> resolveBrush)
    {
        var space = new Border
        {
            Height = ParseDouble(element.Attribute("Height")?.Value) ?? 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = ParseBrush(element.Attribute("Fill")?.Value, resolveBrush)
                ?? resolveBrush("ColorBrushGray6")
        };
        if (ParseDouble(element.Attribute("Width")?.Value) is { } width and > 0d)
        {
            space.Width = width;
            space.HorizontalAlignment = HorizontalAlignment.Left;
        }

        return space;
    }

    private static Control ConvertButton(XElement element, Func<string, IBrush?> resolveBrush)
    {
        var button = new MyButton
        {
            Text = element.Attribute("Content")?.Value ?? element.Value.Trim(),
            Padding = new Thickness(13d, 0d, 13d, 0d),
            MinHeight = 32d
        };
        ApplyCommon(element, button, resolveBrush);
        return button;
    }

    private static void ApplyCommon(
        XElement element,
        Control control,
        Func<string, IBrush?> resolveBrush)
    {
        if (ParseThickness(element.Attribute("Margin")?.Value) is { } margin)
            control.Margin = margin;
        if (ParseDouble(element.Attribute("Width")?.Value) is { } width and > 0d)
            control.Width = width;
        if (ParseDouble(element.Attribute("Height")?.Value) is { } height and > 0d)
            control.Height = height;
        if (ParseDouble(element.Attribute("Opacity")?.Value) is { } opacity)
            control.Opacity = Math.Clamp(opacity, 0d, 1d);

        control.HorizontalAlignment = element.Attribute("HorizontalAlignment")?.Value switch
        {
            "Left" => HorizontalAlignment.Left,
            "Center" => HorizontalAlignment.Center,
            "Right" => HorizontalAlignment.Right,
            "Stretch" => HorizontalAlignment.Stretch,
            _ => control.HorizontalAlignment
        };

        if (control is TextBlock block)
        {
            if (ParseDouble(element.Attribute("FontSize")?.Value) is { } fontSize and > 0d)
                block.FontSize = fontSize;
            if (string.Equals(
                    element.Attribute("FontWeight")?.Value,
                    "Bold",
                    StringComparison.OrdinalIgnoreCase))
                block.FontWeight = FontWeight.Bold;
            if (ParseBrush(element.Attribute("Foreground")?.Value, resolveBrush) is { } foreground)
                block.Foreground = foreground;
        }

        if (control is Border border
            && ParseBrush(element.Attribute("Background")?.Value, resolveBrush) is { } background)
            border.Background = background;
        if (control is Panel panel
            && ParseBrush(element.Attribute("Background")?.Value, resolveBrush) is
                { } panelBackground)
            panel.Background = panelBackground;
    }

    private static IBrush? ParseBrush(string? raw, Func<string, IBrush?> resolveBrush)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var match = DynamicResourcePattern.Match(raw);
        if (match.Success)
            return resolveBrush(match.Groups[1].Value.Trim());

        if (raw.StartsWith('#'))
            try
            {
                return new SolidColorBrush(Color.Parse(raw));
            }
            catch (Exception)
            {
                return null;
            }

        return raw switch
        {
            "Transparent" => Brushes.Transparent,
            "White" => Brushes.White,
            "Black" => Brushes.Black,
            "Red" => Brushes.Red,
            "Blue" => Brushes.Blue,
            "Green" => Brushes.Green,
            "Gray" => Brushes.Gray,
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

    private static double? ParseDouble(string? raw)
    {
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
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
}