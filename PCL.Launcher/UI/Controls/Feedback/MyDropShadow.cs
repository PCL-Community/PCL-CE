using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.UI.Controls;

/// <summary>
///     自绘的九宫格阴影容器。
///     <see cref="Decorator.Child" /> 为内容，阴影绘制在内容所在区域的四周，中心保持透明。
///     四角用径向渐变、四边用线性渐变补齐，避免整体模糊带来的边缘发虚。
/// </summary>
public class MyDropShadow : Decorator
{
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<MyDropShadow, Color>(
            nameof(Color),
            Color.FromArgb(0x71, 0x0, 0x0, 0x0));

    public static readonly StyledProperty<double> ShadowRadiusProperty =
        AvaloniaProperty.Register<MyDropShadow, double>(nameof(ShadowRadius), 5d);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MyDropShadow, CornerRadius>(
            nameof(CornerRadius),
            validate: IsCornerRadiusValid);

    private static IBrush[]? _commonBrushes;
    private static CornerRadius _commonCornerRadius;
    private static readonly Lock ResourceAccess = new();
    private IBrush[]? _brushes;

    static MyDropShadow()
    {
        AffectsRender<MyDropShadow>(ColorProperty, ShadowRadiusProperty, CornerRadiusProperty);
    }

    /// <summary>
    ///     阴影颜色。
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    ///     阴影模糊半径。
    /// </summary>
    public double ShadowRadius
    {
        get => GetValue(ShadowRadiusProperty);
        set => SetValue(ShadowRadiusProperty, value);
    }

    /// <summary>
    ///     圆角大小。
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColorProperty
            || change.Property == ShadowRadiusProperty
            || change.Property == CornerRadiusProperty)
            _brushes = null;
    }

    private static bool IsCornerRadiusValid(CornerRadius cr)
    {
        return !(cr.TopLeft < 0d
            || cr.TopRight < 0d
            || cr.BottomLeft < 0d
            || cr.BottomRight < 0d
            || double.IsNaN(cr.TopLeft)
            || double.IsNaN(cr.TopRight)
            || double.IsNaN(cr.BottomLeft)
            || double.IsNaN(cr.BottomRight)
            || double.IsInfinity(cr.TopLeft)
            || double.IsInfinity(cr.TopRight)
            || double.IsInfinity(cr.BottomLeft)
            || double.IsInfinity(cr.BottomRight));
    }

    // =======================================
    // 渲染
    // =======================================

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var cornerRadius = CornerRadius;
        var shadowBounds = new Rect(0d, 0d, Bounds.Width, Bounds.Height);
        var color = Color;

        if (shadowBounds.Width <= 0d || shadowBounds.Height <= 0d || color.A == 0)
            return;

        var centerWidth = shadowBounds.Right - shadowBounds.Left - 2d * ShadowRadius;
        var centerHeight = shadowBounds.Bottom - shadowBounds.Top - 2d * ShadowRadius;
        var maxRadius = Math.Min(centerWidth * 0.5d, centerHeight * 0.5d);
        cornerRadius = new CornerRadius(
            Math.Min(cornerRadius.TopLeft, maxRadius),
            Math.Min(cornerRadius.TopRight, maxRadius),
            Math.Min(cornerRadius.BottomRight, maxRadius),
            Math.Min(cornerRadius.BottomLeft, maxRadius));

        var brushes = GetBrushes(color, cornerRadius);
        var centerTop = shadowBounds.Top + ShadowRadius;
        var centerLeft = shadowBounds.Left + ShadowRadius;
        var centerRight = shadowBounds.Right - ShadowRadius;
        var centerBottom = shadowBounds.Bottom - ShadowRadius;
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

        var innerRadius = new CornerRadius(
            cornerRadius.TopLeft + ShadowRadius,
            cornerRadius.TopRight + ShadowRadius,
            cornerRadius.BottomRight + ShadowRadius,
            cornerRadius.BottomLeft + ShadowRadius);

        var topLeft = new Rect(
            shadowBounds.Left,
            shadowBounds.Top,
            innerRadius.TopLeft,
            innerRadius.TopLeft);
        context.DrawRectangle(brushes[(int)Placement.TopLeft], null, topLeft);
        var topWidth = guidelineSetX[2] - guidelineSetX[1];

        if (topWidth > 0d)
        {
            var top = new Rect(guidelineSetX[1], shadowBounds.Top, topWidth, ShadowRadius);
            context.DrawRectangle(brushes[(int)Placement.Top], null, top);
        }

        var topRight = new Rect(
            guidelineSetX[2],
            shadowBounds.Top,
            innerRadius.TopRight,
            innerRadius.TopRight);
        context.DrawRectangle(brushes[(int)Placement.TopRight], null, topRight);
        var leftHeight = guidelineSetY[3] - guidelineSetY[1];

        if (leftHeight > 0d)
        {
            var left = new Rect(shadowBounds.Left, guidelineSetY[1], ShadowRadius, leftHeight);
            context.DrawRectangle(brushes[(int)Placement.Left], null, left);
        }

        var rightHeight = guidelineSetY[4] - guidelineSetY[2];

        if (rightHeight > 0d)
        {
            var right = new Rect(guidelineSetX[5], guidelineSetY[2], ShadowRadius, rightHeight);
            context.DrawRectangle(brushes[(int)Placement.Right], null, right);
        }

        var bottomLeft = new Rect(
            shadowBounds.Left,
            guidelineSetY[3],
            innerRadius.BottomLeft,
            innerRadius.BottomLeft);
        context.DrawRectangle(brushes[(int)Placement.BottomLeft], null, bottomLeft);
        var bottomWidth = guidelineSetX[4] - guidelineSetX[3];

        if (bottomWidth > 0d)
        {
            var bottom = new Rect(guidelineSetX[3], guidelineSetY[5], bottomWidth, ShadowRadius);
            context.DrawRectangle(brushes[(int)Placement.Bottom], null, bottom);
        }

        var bottomRight = new Rect(
            guidelineSetX[4],
            guidelineSetY[4],
            innerRadius.BottomRight,
            innerRadius.BottomRight);
        context.DrawRectangle(brushes[(int)Placement.BottomRight], null, bottomRight);

        if (innerRadius.TopLeft == ShadowRadius
            && innerRadius.TopLeft == innerRadius.TopRight
            && innerRadius.TopLeft == innerRadius.BottomLeft
            && innerRadius.TopLeft == innerRadius.BottomRight)
        {
            var center = new Rect(guidelineSetX[0], guidelineSetY[0], centerWidth, centerHeight);
            context.DrawRectangle(brushes[(int)Placement.Center], null, center);
        }
        else
        {
            var geometry = new StreamGeometry();
            using (var geometryContext = geometry.Open())
            {
                if (cornerRadius.TopLeft > ShadowRadius)
                {
                    geometryContext.BeginFigure(new Point(guidelineSetX[1], guidelineSetY[0]));
                    geometryContext.LineTo(new Point(guidelineSetX[1], guidelineSetY[1]));
                    geometryContext.LineTo(new Point(guidelineSetX[0], guidelineSetY[1]));
                }
                else
                {
                    geometryContext.BeginFigure(new Point(guidelineSetX[0], guidelineSetY[0]));
                }

                if (cornerRadius.BottomLeft > ShadowRadius)
                {
                    geometryContext.LineTo(new Point(guidelineSetX[0], guidelineSetY[3]));
                    geometryContext.LineTo(new Point(guidelineSetX[3], guidelineSetY[3]));
                    geometryContext.LineTo(new Point(guidelineSetX[3], guidelineSetY[5]));
                }
                else
                {
                    geometryContext.LineTo(new Point(guidelineSetX[0], guidelineSetY[5]));
                }

                if (cornerRadius.BottomRight > ShadowRadius)
                {
                    geometryContext.LineTo(new Point(guidelineSetX[4], guidelineSetY[5]));
                    geometryContext.LineTo(new Point(guidelineSetX[4], guidelineSetY[4]));
                    geometryContext.LineTo(new Point(guidelineSetX[5], guidelineSetY[4]));
                }
                else
                {
                    geometryContext.LineTo(new Point(guidelineSetX[5], guidelineSetY[5]));
                }

                if (cornerRadius.TopRight > ShadowRadius)
                {
                    geometryContext.LineTo(new Point(guidelineSetX[5], guidelineSetY[2]));
                    geometryContext.LineTo(new Point(guidelineSetX[2], guidelineSetY[2]));
                    geometryContext.LineTo(new Point(guidelineSetX[2], guidelineSetY[0]));
                }
                else
                {
                    geometryContext.LineTo(new Point(guidelineSetX[5], guidelineSetY[0]));
                }

                geometryContext.EndFigure(true);
            }

            context.DrawGeometry(brushes[(int)Placement.Center], null, geometry);
        }
    }

    // =======================================
    // 画刷
    // =======================================

    private GradientStops CreateStops(Color c, double cornerRadius)
    {
        // ShadowRadius 为 0 时半径为 0，对应的边条不会实际绘制；这里避免除零产生非法偏移。
        var denominator = ShadowRadius + cornerRadius;
        var gradientScale = denominator > 0d ? 1d / denominator : 0d;
        var stops = new GradientStops();
        var stopColor = c;
        stops.Add(
            new GradientStop(stopColor, (ShadowRadius * 0.1d + cornerRadius) * gradientScale));
        stopColor = Color.FromArgb((byte)Math.Round(0.74336d * c.A), c.R, c.G, c.B);
        stops.Add(
            new GradientStop(stopColor, (ShadowRadius * 0.3d + cornerRadius) * gradientScale));
        stopColor = Color.FromArgb((byte)Math.Round(0.38053d * c.A), c.R, c.G, c.B);
        stops.Add(
            new GradientStop(stopColor, (ShadowRadius * 0.5d + cornerRadius) * gradientScale));
        stopColor = Color.FromArgb((byte)Math.Round(0.12389d * c.A), c.R, c.G, c.B);
        stops.Add(
            new GradientStop(stopColor, (ShadowRadius * 0.7d + cornerRadius) * gradientScale));
        stopColor = Color.FromArgb((byte)Math.Round(0.02654d * c.A), c.R, c.G, c.B);
        stops.Add(
            new GradientStop(stopColor, (ShadowRadius * 0.9d + cornerRadius) * gradientScale));
        stopColor = Color.FromArgb(0, c.R, c.G, c.B);
        stops.Add(new GradientStop(stopColor, (ShadowRadius + cornerRadius) * gradientScale));
        return stops;
    }

    private IBrush[] CreateBrushes(Color c, CornerRadius cornerRadius)
    {
        var brushes = new IBrush[9];
        brushes[(int)Placement.Center] = new SolidColorBrush(c);
        var sideStops = CreateStops(c, 0d);
        brushes[(int)Placement.Top] = new LinearGradientBrush
        {
            GradientStops = sideStops,
            StartPoint = new RelativePoint(0d, 1d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative)
        };
        brushes[(int)Placement.Left] = new LinearGradientBrush
        {
            GradientStops = sideStops,
            StartPoint = new RelativePoint(1d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative)
        };
        brushes[(int)Placement.Right] = new LinearGradientBrush
        {
            GradientStops = sideStops,
            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1d, 0d, RelativeUnit.Relative)
        };
        brushes[(int)Placement.Bottom] = new LinearGradientBrush
        {
            GradientStops = sideStops,
            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0d, 1d, RelativeUnit.Relative)
        };

        var topLeftStops = cornerRadius.TopLeft == 0d
            ? sideStops
            : CreateStops(c, cornerRadius.TopLeft);
        brushes[(int)Placement.TopLeft] = CreateCornerBrush(topLeftStops, 1d, 1d);

        GradientStops topRightStops;
        if (cornerRadius.TopRight == 0d)
            topRightStops = sideStops;
        else if (cornerRadius.TopRight == cornerRadius.TopLeft)
            topRightStops = topLeftStops;
        else
            topRightStops = CreateStops(c, cornerRadius.TopRight);
        brushes[(int)Placement.TopRight] = CreateCornerBrush(topRightStops, 0d, 1d);

        GradientStops bottomLeftStops;
        if (cornerRadius.BottomLeft == 0d)
            bottomLeftStops = sideStops;
        else if (cornerRadius.BottomLeft == cornerRadius.TopLeft)
            bottomLeftStops = topLeftStops;
        else if (cornerRadius.BottomLeft == cornerRadius.TopRight)
            bottomLeftStops = topRightStops;
        else
            bottomLeftStops = CreateStops(c, cornerRadius.BottomLeft);
        brushes[(int)Placement.BottomLeft] = CreateCornerBrush(bottomLeftStops, 1d, 0d);

        GradientStops bottomRightStops;
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
        brushes[(int)Placement.BottomRight] = CreateCornerBrush(bottomRightStops, 0d, 0d);

        return brushes;
    }

    private static RadialGradientBrush CreateCornerBrush(
        GradientStops stops,
        double centerX,
        double centerY)
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

    private IBrush[] GetBrushes(Color c, CornerRadius cornerRadius)
    {
        if (_commonBrushes is null)
            lock (ResourceAccess)
            {
                if (_commonBrushes is null)
                {
                    _commonBrushes = CreateBrushes(c, cornerRadius);
                    _commonCornerRadius = cornerRadius;
                }
            }

        if (c == ((SolidColorBrush)_commonBrushes[(int)Placement.Center]).Color
            && cornerRadius == _commonCornerRadius)
        {
            _brushes = null;
            return _commonBrushes;
        }

        _brushes ??= CreateBrushes(c, cornerRadius);

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