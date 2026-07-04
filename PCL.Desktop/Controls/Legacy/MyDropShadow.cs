// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

/// <summary>
/// Avalonia adapter for PCL's WPF MyDropShadow chrome.
/// </summary>
public class MyDropShadow : Decorator
{
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<MyDropShadow, Color>(
            nameof(Color),
            Color.FromArgb(0x71, 0x00, 0x00, 0x00));

    public static readonly StyledProperty<double> ShadowRadiusProperty =
        AvaloniaProperty.Register<MyDropShadow, double>(nameof(ShadowRadius), 5d);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MyDropShadow, CornerRadius>(nameof(CornerRadius));

    private IBrush[]? _brushes;
    private Color _brushColor;
    private CornerRadius _brushCornerRadius;
    private double _brushShadowRadius;

    public MyDropShadow()
    {
        this.GetObservable(ColorProperty).Subscribe(_ => ClearBrushes());
        this.GetObservable(ShadowRadiusProperty).Subscribe(_ => ClearBrushes());
        this.GetObservable(CornerRadiusProperty).Subscribe(_ => ClearBrushes());
    }

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double ShadowRadius
    {
        get => GetValue(ShadowRadiusProperty);
        set => SetValue(ShadowRadiusProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var cornerRadius = CornerRadius;
        var shadowBounds = new Rect(0d, 0d, Bounds.Width, Bounds.Height);
        var color = Color;
        var shadowRadius = ShadowRadius;

        if (shadowBounds.Width <= 0d ||
            shadowBounds.Height <= 0d ||
            color.A <= 0 ||
            shadowRadius <= 0d)
        {
            base.Render(context);
            return;
        }

        var centerWidth = shadowBounds.Right - shadowBounds.Left - 2d * shadowRadius;
        var centerHeight = shadowBounds.Bottom - shadowBounds.Top - 2d * shadowRadius;
        if (centerWidth <= 0d || centerHeight <= 0d)
        {
            base.Render(context);
            return;
        }

        var maxRadius = Math.Min(centerWidth * 0.5d, centerHeight * 0.5d);
        cornerRadius = new CornerRadius(
            Math.Min(cornerRadius.TopLeft, maxRadius),
            Math.Min(cornerRadius.TopRight, maxRadius),
            Math.Min(cornerRadius.BottomRight, maxRadius),
            Math.Min(cornerRadius.BottomLeft, maxRadius));

        var brushes = GetBrushes(color, cornerRadius, shadowRadius);
        var centerTop = shadowBounds.Top + shadowRadius;
        var centerLeft = shadowBounds.Left + shadowRadius;
        var centerRight = shadowBounds.Right - shadowRadius;
        var centerBottom = shadowBounds.Bottom - shadowRadius;
        var guidelineSetX = new[]
        {
            centerLeft,
            centerLeft + cornerRadius.TopLeft,
            centerRight - cornerRadius.TopRight,
            centerLeft + cornerRadius.BottomLeft,
            centerRight - cornerRadius.BottomRight,
            centerRight
        };
        var guidelineSetY = new[]
        {
            centerTop,
            centerTop + cornerRadius.TopLeft,
            centerTop + cornerRadius.TopRight,
            centerBottom - cornerRadius.BottomLeft,
            centerBottom - cornerRadius.BottomRight,
            centerBottom
        };

        cornerRadius = new CornerRadius(
            cornerRadius.TopLeft + shadowRadius,
            cornerRadius.TopRight + shadowRadius,
            cornerRadius.BottomRight + shadowRadius,
            cornerRadius.BottomLeft + shadowRadius);

        DrawRectangle(context, brushes[(int)Placement.TopLeft],
            new Rect(shadowBounds.Left, shadowBounds.Top, cornerRadius.TopLeft, cornerRadius.TopLeft));

        var topWidth = guidelineSetX[2] - guidelineSetX[1];
        if (topWidth > 0d)
        {
            DrawRectangle(context, brushes[(int)Placement.Top],
                new Rect(guidelineSetX[1], shadowBounds.Top, topWidth, shadowRadius));
        }

        DrawRectangle(context, brushes[(int)Placement.TopRight],
            new Rect(guidelineSetX[2], shadowBounds.Top, cornerRadius.TopRight, cornerRadius.TopRight));

        var leftHeight = guidelineSetY[3] - guidelineSetY[1];
        if (leftHeight > 0d)
        {
            DrawRectangle(context, brushes[(int)Placement.Left],
                new Rect(shadowBounds.Left, guidelineSetY[1], shadowRadius, leftHeight));
        }

        var rightHeight = guidelineSetY[4] - guidelineSetY[2];
        if (rightHeight > 0d)
        {
            DrawRectangle(context, brushes[(int)Placement.Right],
                new Rect(guidelineSetX[5], guidelineSetY[2], shadowRadius, rightHeight));
        }

        DrawRectangle(context, brushes[(int)Placement.BottomLeft],
            new Rect(shadowBounds.Left, guidelineSetY[3], cornerRadius.BottomLeft, cornerRadius.BottomLeft));

        var bottomWidth = guidelineSetX[4] - guidelineSetX[3];
        if (bottomWidth > 0d)
        {
            DrawRectangle(context, brushes[(int)Placement.Bottom],
                new Rect(guidelineSetX[3], guidelineSetY[5], bottomWidth, shadowRadius));
        }

        DrawRectangle(context, brushes[(int)Placement.BottomRight],
            new Rect(guidelineSetX[4], guidelineSetY[4], cornerRadius.BottomRight, cornerRadius.BottomRight));

        if (cornerRadius.TopLeft == shadowRadius &&
            cornerRadius.TopLeft == cornerRadius.TopRight &&
            cornerRadius.TopLeft == cornerRadius.BottomLeft &&
            cornerRadius.TopLeft == cornerRadius.BottomRight)
        {
            DrawRectangle(context, brushes[(int)Placement.Center],
                new Rect(guidelineSetX[0], guidelineSetY[0], centerWidth, centerHeight));
        }
        else
        {
            var figure = new PathFigure
            {
                Segments = new PathSegments()
            };

            if (cornerRadius.TopLeft > shadowRadius)
            {
                figure.StartPoint = new Point(guidelineSetX[1], guidelineSetY[0]);
                AddLine(figure, guidelineSetX[1], guidelineSetY[1]);
                AddLine(figure, guidelineSetX[0], guidelineSetY[1]);
            }
            else
            {
                figure.StartPoint = new Point(guidelineSetX[0], guidelineSetY[0]);
            }

            if (cornerRadius.BottomLeft > shadowRadius)
            {
                AddLine(figure, guidelineSetX[0], guidelineSetY[3]);
                AddLine(figure, guidelineSetX[3], guidelineSetY[3]);
                AddLine(figure, guidelineSetX[3], guidelineSetY[5]);
            }
            else
            {
                AddLine(figure, guidelineSetX[0], guidelineSetY[5]);
            }

            if (cornerRadius.BottomRight > shadowRadius)
            {
                AddLine(figure, guidelineSetX[4], guidelineSetY[5]);
                AddLine(figure, guidelineSetX[4], guidelineSetY[4]);
                AddLine(figure, guidelineSetX[5], guidelineSetY[4]);
            }
            else
            {
                AddLine(figure, guidelineSetX[5], guidelineSetY[5]);
            }

            if (cornerRadius.TopRight > shadowRadius)
            {
                AddLine(figure, guidelineSetX[5], guidelineSetY[2]);
                AddLine(figure, guidelineSetX[2], guidelineSetY[2]);
                AddLine(figure, guidelineSetX[2], guidelineSetY[0]);
            }
            else
            {
                AddLine(figure, guidelineSetX[5], guidelineSetY[0]);
            }

            figure.IsClosed = true;
            var geometry = new PathGeometry
            {
                Figures = new PathFigures
                {
                    figure
                }
            };
            context.DrawGeometry(brushes[(int)Placement.Center], null, geometry);
        }

        base.Render(context);
    }

    private void ClearBrushes()
    {
        _brushes = null;
        InvalidateVisual();
    }

    private static void DrawRectangle(DrawingContext context, IBrush brush, Rect bounds)
    {
        context.DrawRectangle(brush, null, bounds, 0d, 0d, default);
    }

    private static void AddLine(PathFigure figure, double x, double y)
    {
        (figure.Segments ??= new PathSegments()).Add(new LineSegment { Point = new Point(x, y) });
    }

    private static GradientStops CreateStops(Color color, double cornerRadius, double shadowRadius)
    {
        var gradientScale = 1d / (shadowRadius + cornerRadius);
        var stops = new GradientStops();
        var stopColor = color;

        stops.Add(new GradientStop(stopColor, (shadowRadius * 0.1d + cornerRadius) * gradientScale));
        stopColor = WithAlpha(stopColor, (byte)Math.Round(0.74336d * color.A));
        stops.Add(new GradientStop(stopColor, (shadowRadius * 0.3d + cornerRadius) * gradientScale));
        stopColor = WithAlpha(stopColor, (byte)Math.Round(0.38053d * color.A));
        stops.Add(new GradientStop(stopColor, (shadowRadius * 0.5d + cornerRadius) * gradientScale));
        stopColor = WithAlpha(stopColor, (byte)Math.Round(0.12389d * color.A));
        stops.Add(new GradientStop(stopColor, (shadowRadius * 0.7d + cornerRadius) * gradientScale));
        stopColor = WithAlpha(stopColor, (byte)Math.Round(0.02654d * color.A));
        stops.Add(new GradientStop(stopColor, (shadowRadius * 0.9d + cornerRadius) * gradientScale));
        stopColor = WithAlpha(stopColor, 0);
        stops.Add(new GradientStop(stopColor, (shadowRadius + cornerRadius) * gradientScale));

        return stops;
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static IBrush[] CreateBrushes(Color color, CornerRadius cornerRadius, double shadowRadius)
    {
        var brushes = new IBrush[9];
        brushes[(int)Placement.Center] = new SolidColorBrush(color);

        var sideStops = CreateStops(color, 0d, shadowRadius);
        brushes[(int)Placement.Top] = CreateLinear(sideStops, 0d, 1d, 0d, 0d);
        brushes[(int)Placement.Left] = CreateLinear(sideStops, 1d, 0d, 0d, 0d);
        brushes[(int)Placement.Right] = CreateLinear(sideStops, 0d, 0d, 1d, 0d);
        brushes[(int)Placement.Bottom] = CreateLinear(sideStops, 0d, 0d, 0d, 1d);

        var topLeftStops = cornerRadius.TopLeft == 0d
            ? sideStops
            : CreateStops(color, cornerRadius.TopLeft, shadowRadius);
        brushes[(int)Placement.TopLeft] = CreateRadial(topLeftStops, 1d, 1d);

        var topRightStops = cornerRadius.TopRight == 0d
            ? sideStops
            : cornerRadius.TopRight == cornerRadius.TopLeft
                ? topLeftStops
                : CreateStops(color, cornerRadius.TopRight, shadowRadius);
        brushes[(int)Placement.TopRight] = CreateRadial(topRightStops, 0d, 1d);

        var bottomLeftStops = cornerRadius.BottomLeft == 0d
            ? sideStops
            : cornerRadius.BottomLeft == cornerRadius.TopLeft
                ? topLeftStops
                : cornerRadius.BottomLeft == cornerRadius.TopRight
                    ? topRightStops
                    : CreateStops(color, cornerRadius.BottomLeft, shadowRadius);
        brushes[(int)Placement.BottomLeft] = CreateRadial(bottomLeftStops, 1d, 0d);

        var bottomRightStops = cornerRadius.BottomRight == 0d
            ? sideStops
            : cornerRadius.BottomRight == cornerRadius.TopLeft
                ? topLeftStops
                : cornerRadius.BottomRight == cornerRadius.TopRight
                    ? topRightStops
                    : cornerRadius.BottomRight == cornerRadius.BottomLeft
                        ? bottomLeftStops
                        : CreateStops(color, cornerRadius.BottomRight, shadowRadius);
        brushes[(int)Placement.BottomRight] = CreateRadial(bottomRightStops, 0d, 0d);

        return brushes;
    }

    private static LinearGradientBrush CreateLinear(GradientStops stops, double startX, double startY, double endX, double endY)
    {
        return new LinearGradientBrush
        {
            GradientStops = stops,
            StartPoint = new RelativePoint(startX, startY, RelativeUnit.Relative),
            EndPoint = new RelativePoint(endX, endY, RelativeUnit.Relative)
        };
    }

    private static RadialGradientBrush CreateRadial(GradientStops stops, double centerX, double centerY)
    {
        var center = new RelativePoint(centerX, centerY, RelativeUnit.Relative);
        return new RadialGradientBrush
        {
            GradientStops = stops,
            RadiusX = new RelativeScalar(1d, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(1d, RelativeUnit.Relative),
            Center = center,
            GradientOrigin = center
        };
    }

    private IBrush[] GetBrushes(Color color, CornerRadius cornerRadius, double shadowRadius)
    {
        if (_brushes is not null &&
            _brushColor == color &&
            _brushCornerRadius == cornerRadius &&
            Math.Abs(_brushShadowRadius - shadowRadius) < 0.0001d)
        {
            return _brushes;
        }

        _brushColor = color;
        _brushCornerRadius = cornerRadius;
        _brushShadowRadius = shadowRadius;
        _brushes = CreateBrushes(color, cornerRadius, shadowRadius);
        return _brushes;
    }

    private enum Placement
    {
        TopLeft = 0,
        Top = 1,
        TopRight = 2,
        Left = 3,
        Center = 4,
        Right = 5,
        BottomLeft = 6,
        Bottom = 7,
        BottomRight = 8
    }
}

/// <summary>
/// Compatibility alias for WPF pages that referenced SystemDropShadowChrome directly.
/// </summary>
public sealed class SystemDropShadowChrome : MyDropShadow;
