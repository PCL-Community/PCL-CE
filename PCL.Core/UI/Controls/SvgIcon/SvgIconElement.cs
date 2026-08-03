using System;
using System.Windows.Media;

namespace PCL.Core.UI.Controls.SvgIcon;

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

        context.DrawGeometry(fill, pen, Geometry);
    }

    private Brush? _ResolveFill(SvgIconPaintOptions options)
    {
        var hasFill = _HasPaint(Style.Fill);
        var hasStroke = _HasPaint(Style.Stroke);
        var explicitlyNoFill = _IsNone(Style.Fill);
        Brush? brush;

        if (!options.UseOriginalColor)
        {
            if (explicitlyNoFill)
                return null;

            if (!hasFill && (hasStroke || PreferStrokeByDefault))
                return null;

            brush = options.IconBrush;
        }
        else
        {
            if (explicitlyNoFill)
                return null;

            if (hasFill)
                brush = SvgPaintParser.ParseBrush(Style.Fill, options.IconBrush);
            else if (!hasStroke && !PreferStrokeByDefault)
                brush = Brushes.Black;
            else
                return null;
        }

        return _ApplyOpacity(brush, Style.Opacity * Style.FillOpacity);
    }

    private Pen? _ResolvePen(SvgIconPaintOptions options)
    {
        var hasStroke = _HasPaint(Style.Stroke);
        var explicitlyNoStroke = _IsNone(Style.Stroke);
        Brush? brush;

        if (!options.UseOriginalColor)
        {
            if (explicitlyNoStroke)
                return null;

            if (!hasStroke && !PreferStrokeByDefault)
                return null;

            brush = options.IconBrush;
        }
        else
        {
            if (explicitlyNoStroke)
                return null;

            if (hasStroke)
                brush = SvgPaintParser.ParseBrush(Style.Stroke, options.IconBrush);
            else if (PreferStrokeByDefault)
                brush = Brushes.Black;
            else
                return null;
        }

        return _CreatePen(_ApplyOpacity(brush, Style.Opacity * Style.StrokeOpacity),
            Style.StrokeWidth ?? options.StrokeThickness);
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

    private static Brush? _ApplyOpacity(Brush? brush, double opacity)
    {
        if (brush is null)
            return null;

        opacity = Math.Clamp(opacity, 0D, 1D);
        if (opacity <= 0D)
            return null;

        if (Math.Abs(opacity - 1D) < 0.0001D)
            return brush;

        var clone = brush.CloneCurrentValue();
        clone.Opacity *= opacity;
        if (clone.CanFreeze)
            clone.Freeze();
        return clone;
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