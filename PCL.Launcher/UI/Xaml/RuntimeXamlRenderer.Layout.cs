using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.UI.Controls;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
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

        private Control ConvertBorder(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var border = new Border();
            ApplyCommon(border, element, styles);

            var backgroundElement = FindPropertyElement(element, "Background");
            var brushElement = backgroundElement?.Elements().FirstOrDefault();
            var imageSource = brushElement is null
                ? null
                : FindAttribute(brushElement, "ImageSource")
                ?? FindAttribute(brushElement, "Source");
            var content = ConvertSingleChild(element, styleScope);
            if (!string.IsNullOrWhiteSpace(imageSource))
            {
                var grid = new Grid();
                var image = new MyImage
                {
                    Source = NormalizeImageSource(ResolveString(imageSource) ?? imageSource),
                    Stretch = ParseEnum<Stretch>(FindAttribute(brushElement!, "Stretch"))
                        ?? Stretch.UniformToFill,
                    IsHitTestVisible = false
                };
                grid.Children.Add(image);
                if (content is not null)
                    grid.Children.Add(content);
                border.Child = grid;
                border.ClipToBounds = true;
                return border;
            }

            if (brushElement is not null)
                border.Background = ConvertBrushElement(brushElement) ?? border.Background;

            border.Child = content;
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

            switch (children.Count)
            {
                case 0:
                    return null;
                case 1:
                    return children[0];
            }

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

        private static void ApplyAttachedLayout(
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
    }
}