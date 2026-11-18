using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using PCL.UI.Controls;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private ItemsControl ConvertItemsControl(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var control = new ItemsControl();
            ApplyCommon(control, element, styles);
            AddItemChildren(control.Items, element, styleScope);
            return control;
        }

        private ListBox ConvertListBox(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var control = new ListBox();
            ApplyCommon(control, element, styles);
            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;

                var item = child.Name.LocalName.Equals(
                    "ListBoxItem",
                    StringComparison.OrdinalIgnoreCase)
                    ? ConvertListBoxItem(
                        child,
                        styleScope,
                        styleScope.Resolve("ListBoxItem", child))
                    : ConvertElement(child, styleScope);
                if (item is not null)
                    control.Items.Add(item);
            }

            return control;
        }

        private ListBoxItem ConvertListBoxItem(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var item = new ListBoxItem();
            ApplyCommon(item, element, styles);
            var content = FindAttribute(element, "Content");
            item.Content = content is null
                ? ConvertSingleChild(element, styleScope)
                : ResolveString(content) ?? content;
            return item;
        }

        private Expander ConvertExpander(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var expander = new Expander
            {
                IsExpanded = ParseBool(GetProp(element, styles, "IsExpanded")) ?? false
            };
            ApplyCommon(expander, element, styles);

            var header = FindAttribute(element, "Header");
            if (header is not null)
                expander.Header = ResolveString(header) ?? header;
            expander.Content = ConvertSingleChild(element, styleScope);
            return expander;
        }

        private GroupBox ConvertGroupBox(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var groupBox = new GroupBox();
            ApplyCommon(groupBox, element, styles);

            var header = FindAttribute(element, "Header");
            if (header is not null)
                groupBox.Header = ResolveString(header) ?? header;
            groupBox.Content = ConvertSingleChild(element, styleScope);
            return groupBox;
        }

        private TabControl ConvertTabControl(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var tabControl = new TabControl();
            ApplyCommon(tabControl, element, styles);
            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;

                var item = child.Name.LocalName.Equals(
                    "TabItem",
                    StringComparison.OrdinalIgnoreCase)
                    ? ConvertTabItem(child, styleScope, styleScope.Resolve("TabItem", child))
                    : ConvertElement(child, styleScope);
                if (item is not null)
                    tabControl.Items.Add(item);
            }

            return tabControl;
        }

        private TabItem ConvertTabItem(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var item = new TabItem();
            ApplyCommon(item, element, styles);

            var header = FindAttribute(element, "Header");
            if (header is not null)
                item.Header = ResolveString(header) ?? header;

            var content = FindAttribute(element, "Content");
            item.Content = content is null
                ? ConvertSingleChild(element, styleScope)
                : ResolveString(content) ?? content;
            return item;
        }

        private ProgressBar ConvertProgressBar(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var progress = new ProgressBar
            {
                Minimum = ParseDouble(GetProp(element, styles, "Minimum")) ?? 0d,
                Maximum = ParseDouble(GetProp(element, styles, "Maximum")) ?? 100d,
                Value = ParseDouble(GetProp(element, styles, "Value")) ?? 0d,
                IsIndeterminate =
                    ParseBool(GetProp(element, styles, "IsIndeterminate")) ?? false
            };
            ApplyCommon(progress, element, styles);
            return progress;
        }

        private UniformGrid ConvertUniformGrid(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var grid = new UniformGrid
            {
                Rows = ParseInt(GetProp(element, styles, "Rows")) ?? 0,
                Columns = ParseInt(GetProp(element, styles, "Columns")) ?? 0,
                FirstColumn = ParseInt(GetProp(element, styles, "FirstColumn")) ?? 0
            };
            ApplyCommon(grid, element, styles);
            AddChildren(grid, element, styleScope);
            return grid;
        }

        private Control ConvertCheckBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles,
            bool radio)
        {
            var text = ResolveString(
                    FindAttribute(element, "Content")
                    ?? FindAttribute(element, "Text"))
                ?? "";
            var checkedValue = ParseBool(GetProp(element, styles, "IsChecked"))
                ?? ParseBool(GetProp(element, styles, "Checked"));

            Control control;
            if (radio)
            {
                var box = new MyRadioBox
                {
                    Text = text,
                    Checked = checkedValue ?? false
                };
                ApplyCommon(box, element, styles);
                control = box;
            }
            else
            {
                var box = new MyCheckBox
                {
                    Text = text,
                    Checked = checkedValue
                };
                ApplyCommon(box, element, styles);
                control = box;
            }

            return control;
        }

        private Control ConvertSlider(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var slider = new MySlider
            {
                Value = ParseInt(GetProp(element, styles, "Value")) ?? 0,
                MaxValue = ParseInt(GetProp(element, styles, "Maximum")) ?? 100
            };
            ApplyCommon(slider, element, styles);
            return slider;
        }

        private Control ConvertPolygon(
            XElement element,
            IReadOnlyDictionary<string, string> styles,
            bool polyline)
        {
            var points = ParsePoints(FindAttribute(element, "Points"));
            Shape shape = polyline
                ? new Polyline
                {
                    Points = points
                }
                : new Polygon
                {
                    Points = points
                };
            shape.Fill = FindBrushProperty(element, styles, "Fill");
            shape.Stroke = FindBrushProperty(element, styles, "Stroke");
            shape.StrokeThickness = ParseDouble(GetProp(element, styles, "StrokeThickness")) ?? 1d;
            ApplyCommon(shape, element, styles);
            if (ParseEnum<Stretch>(GetProp(element, styles, "Stretch")) is { } stretch)
                shape.Stretch = stretch;
            return shape;
        }

        private static List<Point> ParsePoints(string? raw)
        {
            var points = new List<Point>();
            if (string.IsNullOrWhiteSpace(raw))
                return points;

            var values = Regex
                .Matches(raw, @"[-+]?(?:\d*\.\d+|\d+)")
                .Select(match => match.Value)
                .ToList();
            for (var index = 0; index + 1 < values.Count; index += 2)
                if (double.TryParse(
                        values[index],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var x)
                    && double.TryParse(
                        values[index + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var y))
                    points.Add(new Point(x, y));

            return points;
        }

        private void AddItemChildren(
            ItemCollection items,
            XElement element,
            StyleScope styleScope)
        {
            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;
                var control = ConvertElement(child, styleScope);
                if (control is not null)
                    items.Add(control);
            }
        }
    }
}