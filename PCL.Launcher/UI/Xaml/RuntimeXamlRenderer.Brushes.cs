using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private static XElement? FindPropertyElement(XElement element, string propertyName)
        {
            return element
                .Elements()
                .FirstOrDefault(child =>
                    child.Name.LocalName.Equals(
                        $"{element.Name.LocalName}.{propertyName}",
                        StringComparison.OrdinalIgnoreCase));
        }

        private IBrush? FindBrushProperty(
            XElement element,
            IReadOnlyDictionary<string, string> styles,
            string propertyName)
        {
            var propertyElement = FindPropertyElement(element, propertyName);
            var brushElement = propertyElement?.Elements().FirstOrDefault();
            return brushElement != null
                ? ConvertBrushElement(brushElement)
                : ParseBrush(GetProp(element, styles, propertyName));
        }

        private IBrush? ConvertBrushElement(XElement element)
        {
            var name = element.Name.LocalName;
            if (name.Equals("LinearGradientBrush", StringComparison.OrdinalIgnoreCase)
                || name.Equals("RadialGradientBrush", StringComparison.OrdinalIgnoreCase))
            {
                var gradient = new LinearGradientBrush
                {
                    StartPoint = ParseRelativePoint(FindAttribute(element, "StartPoint"))
                        ?? new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = ParseRelativePoint(FindAttribute(element, "EndPoint"))
                        ?? new RelativePoint(1, 1, RelativeUnit.Relative)
                };
                foreach (var stop in element
                             .Elements()
                             .Where(child => child.Name.LocalName.Equals(
                                 "GradientStop",
                                 StringComparison.OrdinalIgnoreCase)))
                {
                    var color = ParseColor(FindAttribute(stop, "Color"));
                    if (color is null)
                        continue;
                    var offset = ParseDouble(FindAttribute(stop, "Offset")) ?? 0d;
                    gradient.GradientStops.Add(new GradientStop(color.Value, offset));
                }

                return gradient;
            }

            if (name.Equals("SolidColorBrush", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Color", StringComparison.OrdinalIgnoreCase))
            {
                var color = ParseColor(
                    FindAttribute(element, "Color")
                    ?? element.Value.Trim());
                return color is null ? null : new SolidColorBrush(color.Value);
            }

            return name.Equals("ImageBrush", StringComparison.OrdinalIgnoreCase)
                ? ParseBrush(FindAttribute(element, "FallbackColor"))
                : null;
        }

        private static RelativePoint? ParseRelativePoint(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var parts = raw.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !double.TryParse(
                    parts[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var x)
                || !double.TryParse(
                    parts[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var y))
                return null;

            return new RelativePoint(x, y, RelativeUnit.Relative);
        }

        private static Color? ParseColor(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                return Color.Parse(raw.Trim());
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string? FindPropertyElementValue(XElement element, string propertyName)
        {
            var propertyElement = FindPropertyElement(element, propertyName);
            var child = propertyElement?.Elements().FirstOrDefault();
            return child?.Value.Trim();
        }

        private Geometry? ParseGeometry(string? value)
        {
            var resolved = ResolveString(value);
            if (string.IsNullOrWhiteSpace(resolved))
                return null;

            try
            {
                return Geometry.Parse(resolved);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}