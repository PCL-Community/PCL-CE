// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

internal sealed class SvgIconElement
{
    public required SvgIconElementKind Kind { get; init; }
    public required Geometry Geometry { get; init; }
    public required SvgIconStyle Style { get; init; }

    public bool PreferStrokeByDefault => Kind is SvgIconElementKind.Line or SvgIconElementKind.Polyline;

    public void Draw(DrawingContext context, SvgIconPaintOptions options)
    {
        if (Style.Opacity <= 0d)
            return;

        PaintFill? fill = ResolveFill(options);
        PaintStroke? stroke = ResolveStroke(options);

        if (fill is not null)
        {
            using (context.PushOpacity(fill.Opacity))
                context.DrawGeometry(fill.Brush, null, Geometry);
        }

        if (stroke is not null)
        {
            using (context.PushOpacity(stroke.Opacity))
                context.DrawGeometry(null, stroke.Pen, Geometry);
        }
    }

    private PaintFill? ResolveFill(SvgIconPaintOptions options)
    {
        bool hasFill = HasPaint(Style.Fill);
        bool hasStroke = HasPaint(Style.Stroke);
        bool explicitlyNoFill = IsNone(Style.Fill);
        IBrush? brush;

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

        double opacity = Math.Clamp(Style.Opacity * Style.FillOpacity, 0d, 1d);
        return brush is null || opacity <= 0d ? null : new PaintFill(brush, opacity);
    }

    private PaintStroke? ResolveStroke(SvgIconPaintOptions options)
    {
        bool hasStroke = HasPaint(Style.Stroke);
        bool explicitlyNoStroke = IsNone(Style.Stroke);
        IBrush? brush;

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

        double opacity = Math.Clamp(Style.Opacity * Style.StrokeOpacity, 0d, 1d);
        double thickness = Style.StrokeWidth ?? options.StrokeThickness;
        if (brush is null || opacity <= 0d || thickness <= 0d)
            return null;

        PenLineCap lineCap = ParseLineCap(Style.StrokeLineCap);
        PenLineJoin lineJoin = ParseLineJoin(Style.StrokeLineJoin);
        return new PaintStroke(new Pen(brush, thickness, null, lineCap, lineJoin), opacity);
    }

    private static bool HasPaint(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !IsNone(value);

    private static bool IsNone(string? value) =>
        string.Equals(value?.Trim(), "none", StringComparison.OrdinalIgnoreCase);

    private static PenLineCap ParseLineCap(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "butt" => PenLineCap.Flat,
            "square" => PenLineCap.Square,
            "round" => PenLineCap.Round,
            _ => PenLineCap.Round
        };
    }

    private static PenLineJoin ParseLineJoin(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "miter" => PenLineJoin.Miter,
            "bevel" => PenLineJoin.Bevel,
            "round" => PenLineJoin.Round,
            _ => PenLineJoin.Round
        };
    }

    private sealed record PaintFill(IBrush Brush, double Opacity);
    private sealed record PaintStroke(IPen Pen, double Opacity);
}
