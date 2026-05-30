using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL;

public class MyDropShadow : Decorator
{
    public static readonly DependencyProperty colorProperty = DependencyProperty.Register("Color", typeof(Color),
        typeof(MyDropShadow),
        new FrameworkPropertyMetadata(Color.FromArgb(0x71, 0x0, 0x0, 0x0),
            FrameworkPropertyMetadataOptions.AffectsRender, ClearBrushes));

    public static readonly DependencyProperty shadowRadiusProperty = DependencyProperty.Register("ShadowRadius",
        typeof(double), typeof(MyDropShadow),
        new FrameworkPropertyMetadata(5d, FrameworkPropertyMetadataOptions.AffectsRender, ClearBrushes));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register("CornerRadius",
        typeof(CornerRadius), typeof(MyDropShadow),
        new FrameworkPropertyMetadata(new CornerRadius(), FrameworkPropertyMetadataOptions.AffectsRender, ClearBrushes),
        IsCornerRadiusValid);

    private static Brush[] _commonBrushes;
    private static CornerRadius _commonCornerRadius;
    private static readonly object _resourceAccess = new();
    private Brush[] _brushes;

    /// <summary>
    ///     阴影颜色。
    /// </summary>
    public Color Color
    {
        get => (Color)GetValue(colorProperty);
        set => SetValue(colorProperty, value);
    }

    /// <summary>
    ///     阴影模糊半径。
    /// </summary>
    public double ShadowRadius
    {
        get => (double)GetValue(shadowRadiusProperty);
        set => SetValue(shadowRadiusProperty, value);
    }

    /// <summary>
    ///     圆角大小。
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private static bool IsCornerRadiusValid(object value)
    {
        var cr = (CornerRadius)value;
        return !(cr.TopLeft < 0.0d || cr.TopRight < 0.0d || cr.BottomLeft < 0.0d || cr.BottomRight < 0.0d ||
                 double.IsNaN(cr.TopLeft) || double.IsNaN(cr.TopRight) || double.IsNaN(cr.BottomLeft) ||
                 double.IsNaN(cr.BottomRight) || double.IsInfinity(cr.TopLeft) || double.IsInfinity(cr.TopRight) ||
                 double.IsInfinity(cr.BottomLeft) || double.IsInfinity(cr.BottomRight));
    }


    // =======================================
    // 渲染
    // =======================================


    protected override void OnRender(DrawingContext drawingContext)
    {
        var cornerRadius = CornerRadius;
        var shadowBounds = new Rect(0d, 0d, RenderSize.Width, RenderSize.Height);
        var color = Color;

        if (shadowBounds.Width > 0d && shadowBounds.Height > 0d && color.A > 0)
        {
            var centerWidth = shadowBounds.Right - shadowBounds.Left - 2d * ShadowRadius;
            var centerHeight = shadowBounds.Bottom - shadowBounds.Top - 2d * ShadowRadius;
            var maxRadius = Math.Min(centerWidth * 0.5d, centerHeight * 0.5d);
            cornerRadius.TopLeft = Math.Min(cornerRadius.TopLeft, maxRadius);
            cornerRadius.TopRight = Math.Min(cornerRadius.TopRight, maxRadius);
            cornerRadius.BottomLeft = Math.Min(cornerRadius.BottomLeft, maxRadius);
            cornerRadius.BottomRight = Math.Min(cornerRadius.BottomRight, maxRadius);
            var brushes = GetBrushes(color, cornerRadius);
            var centerTop = shadowBounds.Top + ShadowRadius;
            var centerLeft = shadowBounds.Left + ShadowRadius;
            var centerRight = shadowBounds.Right - ShadowRadius;
            var centerBottom = shadowBounds.Bottom - ShadowRadius;
            var guidelineSetX = new[]
            {
                centerLeft, centerLeft + cornerRadius.TopLeft, centerRight - cornerRadius.TopRight,
                centerLeft + cornerRadius.BottomLeft, centerRight - cornerRadius.BottomRight, centerRight
            };
            var guidelineSetY = new[]
            {
                centerTop, centerTop + cornerRadius.TopLeft, centerTop + cornerRadius.TopRight,
                centerBottom - cornerRadius.BottomLeft, centerBottom - cornerRadius.BottomRight, centerBottom
            };
            drawingContext.PushGuidelineSet(new GuidelineSet(guidelineSetX, guidelineSetY));
            cornerRadius.TopLeft += ShadowRadius;
            cornerRadius.TopRight += ShadowRadius;
            cornerRadius.BottomLeft += ShadowRadius;
            cornerRadius.BottomRight += ShadowRadius;
            var topLeft = new Rect(shadowBounds.Left, shadowBounds.Top, cornerRadius.TopLeft, cornerRadius.TopLeft);
            drawingContext.DrawRectangle(brushes[(int)Placement.TopLeft], null, topLeft);
            var topWidth = guidelineSetX[2] - guidelineSetX[1];

            if (topWidth > 0d)
            {
                var top = new Rect(guidelineSetX[1], shadowBounds.Top, topWidth, ShadowRadius);
                drawingContext.DrawRectangle(brushes[(int)Placement.Top], null, top);
            }

            var topRight = new Rect(guidelineSetX[2], shadowBounds.Top, cornerRadius.TopRight, cornerRadius.TopRight);
            drawingContext.DrawRectangle(brushes[(int)Placement.TopRight], null, topRight);
            var leftHeight = guidelineSetY[3] - guidelineSetY[1];

            if (leftHeight > 0d)
            {
                var left = new Rect(shadowBounds.Left, guidelineSetY[1], ShadowRadius, leftHeight);
                drawingContext.DrawRectangle(brushes[(int)Placement.Left], null, left);
            }

            var rightHeight = guidelineSetY[4] - guidelineSetY[2];

            if (rightHeight > 0d)
            {
                var right = new Rect(guidelineSetX[5], guidelineSetY[2], ShadowRadius, rightHeight);
                drawingContext.DrawRectangle(brushes[(int)Placement.Right], null, right);
            }

            var bottomLeft = new Rect(shadowBounds.Left, guidelineSetY[3], cornerRadius.BottomLeft,
                cornerRadius.BottomLeft);
            drawingContext.DrawRectangle(brushes[(int)Placement.BottomLeft], null, bottomLeft);
            var bottomWidth = guidelineSetX[4] - guidelineSetX[3];

            if (bottomWidth > 0d)
            {
                var bottom = new Rect(guidelineSetX[3], guidelineSetY[5], bottomWidth, ShadowRadius);
                drawingContext.DrawRectangle(brushes[(int)Placement.Bottom], null, bottom);
            }

            var bottomRight = new Rect(guidelineSetX[4], guidelineSetY[4], cornerRadius.BottomRight,
                cornerRadius.BottomRight);
            drawingContext.DrawRectangle(brushes[(int)Placement.BottomRight], null, bottomRight);

            if (cornerRadius.TopLeft == ShadowRadius && cornerRadius.TopLeft == cornerRadius.TopRight &&
                cornerRadius.TopLeft == cornerRadius.BottomLeft && cornerRadius.TopLeft == cornerRadius.BottomRight)
            {
                var center = new Rect(guidelineSetX[0], guidelineSetY[0], centerWidth, centerHeight);
                drawingContext.DrawRectangle(brushes[(int)Placement.Center], null, center);
            }
            else
            {
                var figure = new PathFigure();

                if (cornerRadius.TopLeft > ShadowRadius)
                {
                    figure.StartPoint = new Point(guidelineSetX[1], guidelineSetY[0]);
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[1], guidelineSetY[1]), true));
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[0], guidelineSetY[1]), true));
                }
                else
                {
                    figure.StartPoint = new Point(guidelineSetX[0], guidelineSetY[0]);
                }

                if (cornerRadius.BottomLeft > ShadowRadius)
                {
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[0], guidelineSetY[3]), true));
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[3], guidelineSetY[3]), true));
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[3], guidelineSetY[5]), true));
                }
                else
                {
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[0], guidelineSetY[5]), true));
                }

                if (cornerRadius.BottomRight > ShadowRadius)
                {
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[4], guidelineSetY[5]), true));
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[4], guidelineSetY[4]), true));
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[5], guidelineSetY[4]), true));
                }
                else
                {
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[5], guidelineSetY[5]), true));
                }

                if (cornerRadius.TopRight > ShadowRadius)
                {
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[5], guidelineSetY[2]), true));
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[2], guidelineSetY[2]), true));
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[2], guidelineSetY[0]), true));
                }
                else
                {
                    figure.Segments.Add(new LineSegment(new Point(guidelineSetX[5], guidelineSetY[0]), true));
                }

                figure.IsClosed = true;
                figure.Freeze();
                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                geometry.Freeze();
                drawingContext.DrawGeometry(brushes[(int)Placement.Center], null, geometry);
            }

            drawingContext.Pop();
        }
    }

    private static void ClearBrushes(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        ((MyDropShadow)o)._brushes = null;
    }

    private GradientStopCollection CreateStops(Color c, double cornerRadius)
    {
        var gradientScale = 1d / (ShadowRadius + cornerRadius);
        var gsc = new GradientStopCollection();
        var stopColor = c;
        gsc.Add(new GradientStop(stopColor, (ShadowRadius * 0.1d + cornerRadius) * gradientScale));
        stopColor.A = (byte)Math.Round(0.74336d * c.A);
        gsc.Add(new GradientStop(stopColor, (ShadowRadius * 0.3d + cornerRadius) * gradientScale));
        stopColor.A = (byte)Math.Round(0.38053d * c.A);
        gsc.Add(new GradientStop(stopColor, (ShadowRadius * 0.5d + cornerRadius) * gradientScale));
        stopColor.A = (byte)Math.Round(0.12389d * c.A);
        gsc.Add(new GradientStop(stopColor, (ShadowRadius * 0.7d + cornerRadius) * gradientScale));
        stopColor.A = (byte)Math.Round(0.02654d * c.A);
        gsc.Add(new GradientStop(stopColor, (ShadowRadius * 0.9d + cornerRadius) * gradientScale));
        stopColor.A = 0;
        gsc.Add(new GradientStop(stopColor, (ShadowRadius + cornerRadius) * gradientScale));
        gsc.Freeze();
        return gsc;
    }

    private Brush[] CreateBrushes(Color c, CornerRadius cornerRadius)
    {
        var brushes = new Brush[9];
        brushes[(int)Placement.Center] = new SolidColorBrush(c);
        brushes[(int)Placement.Center].Freeze();
        var sideStops = CreateStops(c, 0d);
        var top = new LinearGradientBrush(sideStops, new Point(0d, 1d), new Point(0d, 0d));
        top.Freeze();
        brushes[(int)Placement.Top] = top;
        var left = new LinearGradientBrush(sideStops, new Point(1d, 0d), new Point(0d, 0d));
        left.Freeze();
        brushes[(int)Placement.Left] = left;
        var right = new LinearGradientBrush(sideStops, new Point(0d, 0d), new Point(1d, 0d));
        right.Freeze();
        brushes[(int)Placement.Right] = right;
        var bottom = new LinearGradientBrush(sideStops, new Point(0d, 0d), new Point(0d, 1d));
        bottom.Freeze();
        brushes[(int)Placement.Bottom] = bottom;
        GradientStopCollection topLeftStops;

        if (cornerRadius.TopLeft == 0d)
            topLeftStops = sideStops;
        else
            topLeftStops = CreateStops(c, cornerRadius.TopLeft);

        var topLeft = new RadialGradientBrush(topLeftStops)
        {
            RadiusX = 1d,
            RadiusY = 1d,
            Center = new Point(1d, 1d),
            GradientOrigin = new Point(1d, 1d)
        };
        topLeft.Freeze();
        brushes[(int)Placement.TopLeft] = topLeft;
        GradientStopCollection topRightStops;

        if (cornerRadius.TopRight == 0d)
            topRightStops = sideStops;
        else if (cornerRadius.TopRight == cornerRadius.TopLeft)
            topRightStops = topLeftStops;
        else
            topRightStops = CreateStops(c, cornerRadius.TopRight);

        var topRight = new RadialGradientBrush(topRightStops)
        {
            RadiusX = 1d,
            RadiusY = 1d,
            Center = new Point(0d, 1d),
            GradientOrigin = new Point(0d, 1d)
        };
        topRight.Freeze();
        brushes[(int)Placement.TopRight] = topRight;
        GradientStopCollection bottomLeftStops;

        if (cornerRadius.BottomLeft == 0d)
            bottomLeftStops = sideStops;
        else if (cornerRadius.BottomLeft == cornerRadius.TopLeft)
            bottomLeftStops = topLeftStops;
        else if (cornerRadius.BottomLeft == cornerRadius.TopRight)
            bottomLeftStops = topRightStops;
        else
            bottomLeftStops = CreateStops(c, cornerRadius.BottomLeft);

        var bottomLeft = new RadialGradientBrush(bottomLeftStops)
        {
            RadiusX = 1d,
            RadiusY = 1d,
            Center = new Point(1d, 0d),
            GradientOrigin = new Point(1d, 0d)
        };
        bottomLeft.Freeze();
        brushes[(int)Placement.BottomLeft] = bottomLeft;
        GradientStopCollection bottomRightStops;

        if (cornerRadius.BottomRight == 0d)
            bottomRightStops = sideStops;
        else if (cornerRadius.BottomRight == cornerRadius.TopLeft)
            bottomRightStops = topLeftStops;
        else if (cornerRadius.BottomRight == cornerRadius.TopRight)
            bottomRightStops = topRightStops;
        else if (cornerRadius.BottomRight == cornerRadius.BottomLeft)
            bottomRightStops = bottomLeftStops;
        else
            bottomRightStops = CreateStops(c, cornerRadius.BottomRight);

        var bottomRight = new RadialGradientBrush(bottomRightStops)
        {
            RadiusX = 1d,
            RadiusY = 1d,
            Center = new Point(0d, 0d),
            GradientOrigin = new Point(0d, 0d)
        };
        bottomRight.Freeze();
        brushes[(int)Placement.BottomRight] = bottomRight;
        return brushes;
    }

    private Brush[] GetBrushes(Color c, CornerRadius cornerRadius)
    {
        if (_commonBrushes is null)
            lock (_resourceAccess)
            {
                if (_commonBrushes is null)
                {
                    _commonBrushes = CreateBrushes(c, cornerRadius);
                    _commonCornerRadius = cornerRadius;
                }
            }

        if (c == ((SolidColorBrush)_commonBrushes[(int)Placement.Center]).Color && cornerRadius == _commonCornerRadius)
        {
            _brushes = null;
            return _commonBrushes;
        }

        if (_brushes is null) _brushes = CreateBrushes(c, cornerRadius);

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

// 参考自：https://referencesource.microsoft.com/#PresentationFramework.Aero/parent/Shared/Microsoft/Windows/Themes/SystemDropShadowChrome.cs,6d9c27d92a8128c1