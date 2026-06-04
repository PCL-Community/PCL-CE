using System;
using System.Windows.Media;

namespace PCL.Core.UI.Icons.Svg;

internal sealed class SvgIconElement
{
    public required SvgIconElementKind Kind { get; init; }
    public required Geometry Geometry { get; init; }
    public required SvgIconStyle Style { get; init; }

    public bool PreferStrokeByDefault => Kind is SvgIconElementKind.Line or SvgIconElementKind.Polyline;

    public void Draw(DrawingContext context, SvgIconPaintOptions options)
    {
        if (Style.Opacity <= 0D)
            return;

        var fill = _ResolveFill(options);
        var pen = _ResolvePen(options);

        if (fill is null && pen is null)
            return;

        if (Style.Opacity < 1D)
            context.PushOpacity(Math.Clamp(Style.Opacity, 0D, 1D));

        context.DrawGeometry(fill, pen, Geometry);

        if (Style.Opacity < 1D)
            context.Pop();
    }

    private Brush? _ResolveFill(SvgIconPaintOptions options)
    {
        var hasFill = _HasPaint(Style.Fill);
        var hasStroke = _HasPaint(Style.Stroke);
        var explicitlyNoFill = _IsNone(Style.Fill);

        if (!options.UseOriginalColor)
        {
            if (explicitlyNoFill)
                return null;

            if (!hasFill && (hasStroke || PreferStrokeByDefault))
                return null;

            return options.IconBrush;
        }

        if (explicitlyNoFill)
            return null;

        if (hasFill)
            return SvgPaintParser.ParseBrush(Style.Fill, options.IconBrush);

        if (!hasStroke && !PreferStrokeByDefault)
            return Brushes.Black;

        return null;
    }

    private Pen? _ResolvePen(SvgIconPaintOptions options)
    {
        var hasStroke = _HasPaint(Style.Stroke);
        var explicitlyNoStroke = _IsNone(Style.Stroke);

        if (!options.UseOriginalColor)
        {
            if (explicitlyNoStroke)
                return null;

            if (!hasStroke && !PreferStrokeByDefault)
                return null;

            return _CreatePen(options.IconBrush, options.StrokeThickness);
        }

        if (explicitlyNoStroke)
            return null;

        if (hasStroke)
            return _CreatePen(SvgPaintParser.ParseBrush(Style.Stroke, options.IconBrush), options.StrokeThickness);

        if (PreferStrokeByDefault)
            return _CreatePen(Brushes.Black, options.StrokeThickness);

        return null;
    }

    private Pen? _CreatePen(Brush? brush, double thickness)
    {
        if (brush is null || thickness <= 0D)
            return null;

        return new Pen(brush, thickness)
        {
            StartLineCap = _ParseLineCap(Style.StrokeLineCap),
            EndLineCap = _ParseLineCap(Style.StrokeLineCap),
            LineJoin = _ParseLineJoin(Style.StrokeLineJoin)
        };
    }

    private static bool _HasPaint(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && !_IsNone(value);
    }

    private static bool _IsNone(string? value)
    {
        return string.Equals(value?.Trim(), "none", StringComparison.OrdinalIgnoreCase);
    }

    private static PenLineCap _ParseLineCap(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "butt" => PenLineCap.Flat,
            "square" => PenLineCap.Square,
            "round" => PenLineCap.Round,
            _ => PenLineCap.Round
        };
    }

    private static PenLineJoin _ParseLineJoin(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "miter" => PenLineJoin.Miter,
            "bevel" => PenLineJoin.Bevel,
            "round" => PenLineJoin.Round,
            _ => PenLineJoin.Round
        };
    }
}