// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using PCL.Desktop.Services;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Controls.Icons;

public sealed class LucideIcon : Control
{
    public static readonly StyledProperty<string?> IconKeyProperty =
        AvaloniaProperty.Register<LucideIcon, string?>(
            nameof(IconKey));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<LucideIcon, IBrush?>(
            nameof(Stroke),
            Brushes.Black);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<LucideIcon, double>(
            nameof(StrokeThickness),
            2);

    private static readonly ConcurrentDictionary<string, LucideIconDocument>
        Cache = new(StringComparer.Ordinal);

    static LucideIcon()
    {
        AffectsRender<LucideIcon>(
            IconKeyProperty,
            StrokeProperty,
            StrokeThicknessProperty);
    }

    public string? IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Stroke is null ||
            string.IsNullOrWhiteSpace(IconKey) ||
            Bounds.Width <= 0 ||
            Bounds.Height <= 0)
        {
            return;
        }

        LucideIconDocument document = Cache.GetOrAdd(
            IconKey,
            static key => LucideIconDocument.Load(key));
        double scale = Math.Min(Bounds.Width, Bounds.Height) / 24d;
        double offsetX = (Bounds.Width - 24d * scale) / 2d;
        double offsetY = (Bounds.Height - 24d * scale) / 2d;
        Pen pen = new(
            Stroke,
            StrokeThickness,
            null,
            PenLineCap.Round,
            PenLineJoin.Round,
            10);

        using (context.PushTransform(
                   Matrix.CreateTranslation(offsetX, offsetY)))
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            document.Draw(context, pen);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width)
            ? 18
            : Math.Min(18, availableSize.Width);
        double height = double.IsInfinity(availableSize.Height)
            ? 18
            : Math.Min(18, availableSize.Height);
        return new Size(width, height);
    }

    private sealed class LucideIconDocument(
        IReadOnlyList<LucideShape> shapes)
    {
        public static LucideIconDocument Load(string key)
        {
            IconResource? resource =
                AvaloniaIconService.Shared.GetIcon(key);
            if (resource is null)
                return new LucideIconDocument([]);

            using Stream stream = AssetLoader.Open(resource.ResourceUri);
            XDocument document = XDocument.Load(
                stream,
                LoadOptions.None);
            XElement? root = document.Root;
            if (root is null)
                return new LucideIconDocument([]);

            List<LucideShape> shapes = [];
            foreach (XElement element in root.Elements())
            {
                LucideShape? shape = element.Name.LocalName switch
                {
                    "path" => CreatePath(element),
                    "line" => CreateLine(element),
                    "polyline" => CreatePolyline(element, false),
                    "polygon" => CreatePolyline(element, true),
                    "circle" => CreateCircle(element),
                    "ellipse" => CreateEllipse(element),
                    "rect" => CreateRectangle(element),
                    _ => null
                };
                if (shape is not null)
                    shapes.Add(shape);
            }

            return new LucideIconDocument(shapes);
        }

        public void Draw(DrawingContext context, Pen pen)
        {
            foreach (LucideShape shape in shapes)
                shape.Draw(context, pen);
        }

        private static GeometryShape? CreatePath(XElement element)
        {
            string? data = (string?)element.Attribute("d");
            return string.IsNullOrWhiteSpace(data)
                ? null
                : new GeometryShape(Geometry.Parse(data));
        }

        private static LineShape CreateLine(XElement element) =>
            new LineShape(
                new Point(
                    GetDouble(element, "x1"),
                    GetDouble(element, "y1")),
                new Point(
                    GetDouble(element, "x2"),
                    GetDouble(element, "y2")));

        private static GeometryShape? CreatePolyline(
            XElement element,
            bool closed)
        {
            string? raw = (string?)element.Attribute("points");
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            double[] values = raw
                .Split(
                    [' ', ',', '\r', '\n', '\t'],
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    static value =>
                        double.Parse(
                            value,
                            CultureInfo.InvariantCulture))
                .ToArray();
            if (values.Length < 4 || values.Length % 2 != 0)
                return null;

            Point[] points = new Point[values.Length / 2];
            for (int index = 0; index < points.Length; index++)
            {
                points[index] = new Point(
                    values[index * 2],
                    values[index * 2 + 1]);
            }

            return new GeometryShape(
                new PolylineGeometry(points, closed));
        }

        private static EllipseShape CreateCircle(XElement element) =>
            new EllipseShape(
                new Point(
                    GetDouble(element, "cx"),
                    GetDouble(element, "cy")),
                GetDouble(element, "r"),
                GetDouble(element, "r"));

        private static EllipseShape CreateEllipse(XElement element) =>
            new EllipseShape(
                new Point(
                    GetDouble(element, "cx"),
                    GetDouble(element, "cy")),
                GetDouble(element, "rx"),
                GetDouble(element, "ry"));

        private static RectangleShape CreateRectangle(XElement element) =>
            new RectangleShape(
                new Rect(
                    GetDouble(element, "x"),
                    GetDouble(element, "y"),
                    GetDouble(element, "width"),
                    GetDouble(element, "height")),
                GetDouble(element, "rx"));

        private static double GetDouble(
            XElement element,
            string attributeName)
        {
            string? value = (string?)element.Attribute(attributeName);
            return string.IsNullOrWhiteSpace(value)
                ? 0
                : double.Parse(value, CultureInfo.InvariantCulture);
        }
    }

    private abstract record LucideShape
    {
        public abstract void Draw(DrawingContext context, Pen pen);
    }

    private sealed record GeometryShape(Geometry Geometry) : LucideShape
    {
        public override void Draw(DrawingContext context, Pen pen) =>
            context.DrawGeometry(null, pen, Geometry);
    }

    private sealed record LineShape(Point Start, Point End) : LucideShape
    {
        public override void Draw(DrawingContext context, Pen pen) =>
            context.DrawLine(pen, Start, End);
    }

    private sealed record EllipseShape(
        Point Center,
        double RadiusX,
        double RadiusY) : LucideShape
    {
        public override void Draw(DrawingContext context, Pen pen) =>
            context.DrawEllipse(null, pen, Center, RadiusX, RadiusY);
    }

    private sealed record RectangleShape(
        Rect Bounds,
        double Radius) : LucideShape
    {
        public override void Draw(DrawingContext context, Pen pen) =>
            context.DrawRectangle(
                null,
                pen,
                Bounds,
                Radius,
                Radius);
    }
}
