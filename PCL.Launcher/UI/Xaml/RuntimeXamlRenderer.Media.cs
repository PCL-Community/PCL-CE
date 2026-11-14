using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.UI.Controls;
using AvaloniaEllipse = Avalonia.Controls.Shapes.Ellipse;
using AvaloniaLine = Avalonia.Controls.Shapes.Line;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using AvaloniaRectangle = Avalonia.Controls.Shapes.Rectangle;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private MyImage ConvertImage(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var image = new MyImage();
            ApplyCommon(image, element, styles);
            ApplyMyImageProperties(image, element, styles);
            return image;
        }

        private Control ConvertShape(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var localName = element.Name.LocalName;
            var fill = FindBrushProperty(element, styles, "Fill");
            var stroke = FindBrushProperty(element, styles, "Stroke");
            var strokeThickness = ParseDouble(GetProp(element, styles, "StrokeThickness"));
            if (localName.Equals("Line", StringComparison.OrdinalIgnoreCase))
            {
                var line = new AvaloniaLine
                {
                    StartPoint = new Point(
                        ParseDouble(GetProp(element, styles, "X1")) ?? 0d,
                        ParseDouble(GetProp(element, styles, "Y1")) ?? 0d),
                    EndPoint = new Point(
                        ParseDouble(GetProp(element, styles, "X2")) ?? 0d,
                        ParseDouble(GetProp(element, styles, "Y2")) ?? 0d),
                    Stroke = stroke ?? fill ?? Brushes.Transparent,
                    StrokeThickness = strokeThickness ?? 1d
                };
                ApplyCommon(line, element, styles);
                return line;
            }

            if (localName.Equals("Ellipse", StringComparison.OrdinalIgnoreCase))
            {
                var ellipse = new AvaloniaEllipse
                {
                    Fill = fill,
                    Stroke = stroke,
                    StrokeThickness = strokeThickness ?? 0d
                };
                ApplyCommon(ellipse, element, styles);
                return ellipse;
            }

            var rectangle = new AvaloniaRectangle
            {
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = strokeThickness ?? 0d,
                RadiusX = ParseDouble(GetProp(element, styles, "RadiusX")) ?? 0d,
                RadiusY = ParseDouble(GetProp(element, styles, "RadiusY")) ?? 0d
            };
            ApplyCommon(rectangle, element, styles);
            return rectangle;
        }

        private Control ConvertPath(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var path = new AvaloniaPath
            {
                Data = ParseGeometry(
                    FindPropertyElementValue(element, "Data")
                    ?? GetProp(element, styles, "Data")),
                Fill = FindBrushProperty(element, styles, "Fill"),
                Stroke = FindBrushProperty(element, styles, "Stroke"),
                StrokeThickness = ParseDouble(GetProp(element, styles, "StrokeThickness")) ?? 0d
            };
            ApplyCommon(path, element, styles);
            if (ParseEnum<Stretch>(GetProp(element, styles, "Stretch")) is { } stretch)
                path.Stretch = stretch;
            return path;
        }
    }
}