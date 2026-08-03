using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using PCL.Core.Logging;

namespace PCL.Core.UI.Controls.SvgIcon;

internal static class SvgIconParser
{
    public static SvgIconModel Parse(string svg)
    {
        using var stringReader = new StringReader(svg);
        using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        var document = XDocument.Load(xmlReader, LoadOptions.None);
        var root = document.Root ?? throw new FormatException("SVG 文件缺少根节点。");

        var (minX, minY, width, height) = _ReadViewBox(root);
        var elements = new List<SvgIconElement>();

        _ReadElements(root, elements, new SvgIconStyle());

        return new SvgIconModel
        {
            MinX = minX,
            MinY = minY,
            Width = width <= 0D ? 24D : width,
            Height = height <= 0D ? 24D : height,
            Elements = elements
        };
    }

    private static void _ReadElements(
        XElement parent,
        ICollection<SvgIconElement> result,
        SvgIconStyle inheritedStyle)
    {
        var parentStyle = inheritedStyle.Merge(parent);

        foreach (var element in parent.Elements())
        {
            var name = element.Name.LocalName;

            if (name is "g" or "svg")
            {
                _ReadElements(element, result, parentStyle);
                continue;
            }

            var style = parentStyle.Merge(element);
            var iconElement = _CreateElement(name, element, style);
            if (iconElement is not null)
                result.Add(iconElement);
        }
    }

    private static SvgIconElement? _CreateElement(string name, XElement element, SvgIconStyle style)
    {
        try
        {
            return name switch
            {
                "path" => _CreatePath(element, style),
                "line" => _CreateLine(element, style),
                "circle" => _CreateCircle(element, style),
                "ellipse" => _CreateEllipse(element, style),
                "rect" => _CreateRect(element, style),
                "polyline" => _CreatePolyline(element, style),
                "polygon" => _CreatePolygon(element, style),
                _ => null
            };
        }
        catch (Exception ex)
        {
#if DEBUG
            var descriptor = name;
            var d = _Attr(element, "d");
            if (!string.IsNullOrWhiteSpace(d))
                descriptor += $" d=\"{(d.Length > 80 ? d[..80] + "..." : d)}\"";

            LogWrapper.Debug(ex, "SvgIcon", $"跳过无法解析的 SVG 元素：{descriptor}");
#endif
            // 单个图元解析失败时跳过，避免一个不兼容节点导致整个图标不可用。
            return null;
        }
    }

    private static SvgIconElement? _CreatePath(XElement element, SvgIconStyle style)
    {
        var d = _Attr(element, "d");
        if (string.IsNullOrWhiteSpace(d))
            return null;

        var geometry = SvgPathGeometryParser.Parse(d);
        _ApplyFillRule(geometry, style);
        _TryFreeze(geometry);

        return new SvgIconElement
        {
            Kind = SvgIconElementKind.Path,
            Geometry = geometry,
            Style = style
        };
    }

    private static SvgIconElement _CreateLine(XElement element, SvgIconStyle style)
    {
        var geometry = new LineGeometry(
            new Point(_Number(element, "x1"), _Number(element, "y1")),
            new Point(_Number(element, "x2"), _Number(element, "y2")));

        _TryFreeze(geometry);

        return new SvgIconElement
        {
            Kind = SvgIconElementKind.Line,
            Geometry = geometry,
            Style = style
        };
    }

    private static SvgIconElement _CreateCircle(XElement element, SvgIconStyle style)
    {
        var geometry = new EllipseGeometry(
            new Point(_Number(element, "cx"), _Number(element, "cy")),
            _Number(element, "r"),
            _Number(element, "r"));

        _TryFreeze(geometry);

        return new SvgIconElement
        {
            Kind = SvgIconElementKind.Circle,
            Geometry = geometry,
            Style = style
        };
    }

    private static SvgIconElement _CreateEllipse(XElement element, SvgIconStyle style)
    {
        var geometry = new EllipseGeometry(
            new Point(_Number(element, "cx"), _Number(element, "cy")),
            _Number(element, "rx"),
            _Number(element, "ry"));

        _TryFreeze(geometry);

        return new SvgIconElement
        {
            Kind = SvgIconElementKind.Ellipse,
            Geometry = geometry,
            Style = style
        };
    }

    private static SvgIconElement _CreateRect(XElement element, SvgIconStyle style)
    {
        var x = _Number(element, "x");
        var y = _Number(element, "y");
        var width = _Number(element, "width");
        var height = _Number(element, "height");
        var rx = _Number(element, "rx", _Number(element, "ry"));
        var ry = _Number(element, "ry", rx);

        var geometry = new RectangleGeometry(new Rect(x, y, width, height), rx, ry);
        _TryFreeze(geometry);

        return new SvgIconElement
        {
            Kind = SvgIconElementKind.Rect,
            Geometry = geometry,
            Style = style
        };
    }

    private static SvgIconElement? _CreatePolyline(XElement element, SvgIconStyle style)
    {
        var geometry = _CreatePointsGeometry(_Attr(element, "points"), false);
        if (geometry is null)
            return null;

        return new SvgIconElement
        {
            Kind = SvgIconElementKind.Polyline,
            Geometry = geometry,
            Style = style
        };
    }

    private static SvgIconElement? _CreatePolygon(XElement element, SvgIconStyle style)
    {
        var geometry = _CreatePointsGeometry(_Attr(element, "points"), true);
        if (geometry is null)
            return null;

        return new SvgIconElement
        {
            Kind = SvgIconElementKind.Polygon,
            Geometry = geometry,
            Style = style
        };
    }

    private static StreamGeometry? _CreatePointsGeometry(string? points, bool close)
    {
        var numbers = SvgNumberParser.ParseNumberList(points);
        if (numbers.Length < 4)
            return null;

        var geometry = new StreamGeometry
        {
            FillRule = close ? FillRule.Nonzero : FillRule.EvenOdd
        };
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(numbers[0], numbers[1]), close, close);
            for (var i = 2; i + 1 < numbers.Length; i += 2)
                context.LineTo(new Point(numbers[i], numbers[i + 1]), true, false);
        }

        _TryFreeze(geometry);
        return geometry;
    }


    private static (double MinX, double MinY, double Width, double Height) _ReadViewBox(XElement root)
    {
        var viewBox = _Attr(root, "viewBox");
        var numbers = SvgNumberParser.ParseNumberList(viewBox);
        if (numbers.Length == 4)
            return (numbers[0], numbers[1], numbers[2], numbers[3]);

        var width = SvgNumberParser.TryParseNullable(_Attr(root, "width")) ?? 24D;
        var height = SvgNumberParser.TryParseNullable(_Attr(root, "height")) ?? 24D;
        return (0D, 0D, width, height);
    }

    private static double _Number(XElement element, string name, double fallback = 0D)
    {
        return SvgNumberParser.TryParse(_Attr(element, name), fallback);
    }

    private static string? _Attr(XElement element, string name)
    {
        return element.Attribute(name)?.Value;
    }

    private static void _ApplyFillRule(Geometry geometry, SvgIconStyle style)
    {
        var fillRule = style.FillRule?.Trim().ToLowerInvariant() switch
        {
            "evenodd" => FillRule.EvenOdd,
            _ => FillRule.Nonzero
        };

        switch (geometry)
        {
            case StreamGeometry streamGeometry:
                streamGeometry.FillRule = fillRule;
                break;
            case PathGeometry pathGeometry:
                pathGeometry.FillRule = fillRule;
                break;
        }
    }

    private static void _TryFreeze(Freezable freezable)
    {
        if (freezable.CanFreeze)
            freezable.Freeze();
    }
}