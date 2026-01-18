using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PCL.Core.UI.Effects;

// 该部分源码来自或修改于 https://github.com/OrgEleCho/EleCho.WpfSuite
// 项目: EleCho.WpfSuite
// 作者: EleCho
// 协议: MIT License

namespace PCL.Core.UI.Controls;

// ReSharper disable All

public class BlurBorder : Border
{

    private const double DoubleEpsilon = 2.2204460492503131e-016;

    private static bool _IsZero(double value) => Math.Abs(value) < 10.0 * DoubleEpsilon;

    private readonly Stack<UIElement> _panelStack = new();

    /// <summary>
    /// A geometry to clip the content of this border correctly
    /// </summary>
    public Geometry? ContentClip
    {
        get { return (Geometry)GetValue(ContentClipProperty); }
        set { SetValue(ContentClipProperty, value); }
    }

    /// <summary>
    /// Gets or sets the maximum depth of the visual tree to render.
    /// </summary>
    public int MaxDepth
    {
        get { return (int)GetValue(MaxDepthProperty); }
        set { SetValue(MaxDepthProperty, value); }
    }

    /// <summary>
    /// Gets or sets the radius of the blur effect applied to the background.
    /// </summary>
    public double BlurRadius
    {
        get { return (double)GetValue(BlurRadiusProperty); }
        set { SetValue(BlurRadiusProperty, value); }
    }

    /// <summary>
    /// Gets or sets the type of kernel used for the blur effect.
    /// </summary>
    public KernelType BlurKernelType
    {
        get { return (KernelType)GetValue(BlurKernelTypeProperty); }
        set { SetValue(BlurKernelTypeProperty, value); }
    }

    /// <summary>
    /// Gets or sets the rendering bias for the blur effect, which can affect performance and quality.
    /// </summary>
    public RenderingBias BlurRenderingBias
    {
        get { return (RenderingBias)GetValue(BlurRenderingBiasProperty); }
        set { SetValue(BlurRenderingBiasProperty, value); }
    }

    /// <summary>
    /// Gets or sets the sampling rate for blur effect (0.1-1.0).
    /// Lower values significantly improve performance: 0.3 = 70% performance boost.
    /// Default is 0.7 for balanced quality and performance.
    /// </summary>
    public double BlurSamplingRate
    {
        get { return (double)GetValue(BlurSamplingRateProperty); }
        set { SetValue(BlurSamplingRateProperty, Math.Max(0.1, Math.Min(1.0, value))); }
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        SetValue(ContentClipPropertyKey, CalculateContentClip(this));

        return base.ArrangeOverride(finalSize);
    }

    /// <inheritdoc/>
    protected override Geometry? GetLayoutClip(Size layoutSlotSize)
    {
        if (!ClipToBounds)
        {
            return null;
        }

        return CalculateLayoutClip(layoutSlotSize, BorderThickness, CornerRadius);
    }

    /// <inheritdoc/>
    protected override void OnVisualParentChanged(DependencyObject oldParentObject)
    {
        if (oldParentObject is UIElement oldParent)
        {
            oldParent.LayoutUpdated -= ParentLayoutUpdated;
        }

        if (Parent is UIElement newParent)
        {
            newParent.LayoutUpdated += ParentLayoutUpdated;
        }
    }

    private void ParentLayoutUpdated(object? sender, EventArgs e)
    {
        // cannot use 'InvalidateVisual' here, because it will cause infinite loop

        BackgroundPresenter.ForceRender(this);

        // Debug.WriteLine("Parent layout updated, forcing render of BackgroundPresenter.");
    }

    private static Geometry? CalculateContentClip(Border border)
    {
        var borderThickness = border.BorderThickness;
        var cornerRadius = border.CornerRadius;
        var renderSize = border.RenderSize;

        var contentWidth = renderSize.Width - borderThickness.Left - borderThickness.Right;
        var contentHeight = renderSize.Height - borderThickness.Top - borderThickness.Bottom;

        if (contentWidth > 0 && contentHeight > 0)
        {
            var rect = new Rect(0, 0, contentWidth, contentHeight);
            var radii = new Radii(cornerRadius, borderThickness, false);

            var contentGeometry = new StreamGeometry();
            using StreamGeometryContext ctx = contentGeometry.Open();
            GenerateGeometry(ctx, rect, radii);

            contentGeometry.Freeze();
            return contentGeometry;
        }
        else
        {
            return null;
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext dc)
    {
        // 防止无意义渲染
        if (BlurRadius == 0
            || Opacity == 0
            || Visibility is Visibility.Collapsed or Visibility.Hidden)
        {
            base.OnRender(dc);
            return;
        }
        
        DrawingVisual drawingVisual = new DrawingVisual()
        {
            Clip = new RectangleGeometry(new Rect(0, 0, RenderSize.Width, RenderSize.Height)),
            Effect = CreateOptimizedBlurEffect()
        };

        using (DrawingContext visualContext = drawingVisual.RenderOpen())
        {
            BackgroundPresenter.DrawBackground(visualContext, this, _panelStack, MaxDepth, false);
        }

        if (drawingVisual.Drawing is not null)
        {
            var layoutClip = CalculateLayoutClip(RenderSize, BorderThickness, CornerRadius);
            if (layoutClip != null)
            {
                dc.PushClip(layoutClip);
            }

            BackgroundPresenter.DrawVisual(dc, drawingVisual, default);

            if (layoutClip != null)
            {
                dc.Pop();
            }
        }

        base.OnRender(dc);
    }

    /// <summary>
    /// 创建优化的模糊效果实例
    /// </summary>
    private Effect CreateOptimizedBlurEffect()
    {
        // 根据模糊半径和采样率智能选择算法
        if (BlurRadius <= 2.0 || BlurSamplingRate >= 0.95)
        {
            // 小半径或高采样率：使用原生 BlurEffect 获得最佳质量
            return new BlurEffect
            {
                Radius = BlurRadius,
                KernelType = BlurKernelType,
                RenderingBias = BlurRenderingBias
            };
        }
        else if (BlurRadius >= 50.0 && BlurSamplingRate <= 0.3)
        {
            // 大半径低采样率：使用极速优化版本
            var ultraFastBlur = OptimizedBlurFactory.CreateRealTimePreview(BlurRadius);
            ultraFastBlur.SamplingRate = Math.Max(0.1, BlurSamplingRate);
            ultraFastBlur.RenderingBias = RenderingBias.Performance;
            return ultraFastBlur.GetEffectInstance();
        }
        else
        {
            // 中等情况：使用我们的自适应优化算法
            var adaptiveBlur = OptimizedBlurFactory.CreateAdaptive(BlurRadius);
            adaptiveBlur.SamplingRate = BlurSamplingRate;
            adaptiveBlur.RenderingBias = BlurRenderingBias;
            adaptiveBlur.KernelType = BlurKernelType;
            return adaptiveBlur.GetEffectInstance();
        }
    }

    /// <summary>
    /// The key needed set a read-only property
    /// </summary>
    private static readonly DependencyPropertyKey ContentClipPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ContentClip), typeof(Geometry), typeof(BlurBorder), new FrameworkPropertyMetadata(default(Geometry)));

    /// <summary>
    /// The DependencyProperty for the ContentClip property. <br/>
    /// Flags: None <br/>
    /// Default value: null
    /// </summary>
    public static readonly DependencyProperty ContentClipProperty =
        ContentClipPropertyKey.DependencyProperty;

    /// <summary>
    /// The maximum depth of the visual tree to render.
    /// </summary>
    public static readonly DependencyProperty MaxDepthProperty =
        BackgroundPresenter.MaxDepthProperty.AddOwner(typeof(BlurBorder));

    /// <summary>
    /// The radius of the blur effect applied to the background.
    /// </summary>
    public static readonly DependencyProperty BlurRadiusProperty =
        DependencyProperty.Register(nameof(BlurRadius), typeof(double), typeof(BlurBorder), new FrameworkPropertyMetadata(16.0, propertyChangedCallback: OnRenderPropertyChanged));

    /// <summary>
    /// The type of kernel used for the blur effect.
    /// </summary>
    public static readonly DependencyProperty BlurKernelTypeProperty =
        DependencyProperty.Register(nameof(BlurKernelType), typeof(KernelType), typeof(BlurBorder), new FrameworkPropertyMetadata(KernelType.Gaussian, propertyChangedCallback: OnRenderPropertyChanged));

    /// <summary>
    /// The rendering bias for the blur effect, which can affect performance and quality.
    /// </summary>
    public static readonly DependencyProperty BlurRenderingBiasProperty =
        DependencyProperty.Register(nameof(BlurRenderingBias), typeof(RenderingBias), typeof(BlurBorder), new FrameworkPropertyMetadata(RenderingBias.Performance, propertyChangedCallback: OnRenderPropertyChanged));

    /// <summary>
    /// The sampling rate for blur effect, controlling performance vs quality trade-off.
    /// </summary>
    public static readonly DependencyProperty BlurSamplingRateProperty =
        DependencyProperty.Register(nameof(BlurSamplingRate), typeof(double), typeof(BlurBorder), new FrameworkPropertyMetadata(0.9, propertyChangedCallback: OnRenderPropertyChanged));

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            BackgroundPresenter.ForceRender(element);
        }
    }

    /// <summary>
    ///     Generates a StreamGeometry.
    /// </summary>
    /// <param name="ctx">An already opened StreamGeometryContext.</param>
    /// <param name="rect">Rectangle for geometry conversion.</param>
    /// <param name="radii">Corner radii.</param>
    /// <returns>Result geometry.</returns>
    internal static void GenerateGeometry(StreamGeometryContext ctx, Rect rect, Radii radii)
    {
        //
        //  compute the coordinates of the key points
        //

        Point topLeft = new Point(radii.LeftTop, 0);
        Point topRight = new Point(rect.Width - radii.RightTop, 0);
        Point rightTop = new Point(rect.Width, radii.TopRight);
        Point rightBottom = new Point(rect.Width, rect.Height - radii.BottomRight);
        Point bottomRight = new Point(rect.Width - radii.RightBottom, rect.Height);
        Point bottomLeft = new Point(radii.LeftBottom, rect.Height);
        Point leftBottom = new Point(0, rect.Height - radii.BottomLeft);
        Point leftTop = new Point(0, radii.TopLeft);
        
        //
        //  check key points for overlap and resolve by partitioning radii according to
        //  the percentage of each one.  
        //

        //  top edge is handled here
        if (topLeft.X > topRight.X)
        {
            double v = (radii.LeftTop) / (radii.LeftTop + radii.RightTop) * rect.Width;
            topLeft.X = v;
            topRight.X = v;
        }

        //  right edge
        if (rightTop.Y > rightBottom.Y)
        {
            double v = (radii.TopRight) / (radii.TopRight + radii.BottomRight) * rect.Height;
            rightTop.Y = v;
            rightBottom.Y = v;
        }

        //  bottom edge
        if (bottomRight.X < bottomLeft.X)
        {
            double v = (radii.LeftBottom) / (radii.LeftBottom + radii.RightBottom) * rect.Width;
            bottomRight.X = v;
            bottomLeft.X = v;
        }

        // left edge
        if (leftBottom.Y < leftTop.Y)
        {
            double v = (radii.TopLeft) / (radii.TopLeft + radii.BottomLeft) * rect.Height;
            leftBottom.Y = v;
            leftTop.Y = v;
        }

        //
        //  add on offsets
        //

        Vector offset = new Vector(rect.TopLeft.X, rect.TopLeft.Y);
        topLeft += offset;
        topRight += offset;
        rightTop += offset;
        rightBottom += offset;
        bottomRight += offset;
        bottomLeft += offset;
        leftBottom += offset;
        leftTop += offset;

        //
        //  create the border geometry
        //
        ctx.BeginFigure(topLeft, true /* is filled */, true /* is closed */);

        // Top line
        ctx.LineTo(topRight, true /* is stroked */, false /* is smooth join */);

        // Upper-right corner
        double radiusX = rect.TopRight.X - topRight.X;
        double radiusY = rightTop.Y - rect.TopRight.Y;
        if (!_IsZero(radiusX)
            || !_IsZero(radiusY))
        {
            ctx.ArcTo(rightTop, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }

        // Right line
        ctx.LineTo(rightBottom, true /* is stroked */, false /* is smooth join */);

        // Lower-right corner
        radiusX = rect.BottomRight.X - bottomRight.X;
        radiusY = rect.BottomRight.Y - rightBottom.Y;
        if (!_IsZero(radiusX)
            || !_IsZero(radiusY))
        {
            ctx.ArcTo(bottomRight, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }

        // Bottom line
        ctx.LineTo(bottomLeft, true /* is stroked */, false /* is smooth join */);

        // Lower-left corner
        radiusX = bottomLeft.X - rect.BottomLeft.X;
        radiusY = rect.BottomLeft.Y - leftBottom.Y;
        if (!_IsZero(radiusX)
            || !_IsZero(radiusY))
        {
            ctx.ArcTo(leftBottom, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }

        // Left line
        ctx.LineTo(leftTop, true /* is stroked */, false /* is smooth join */);

        // Upper-left corner
        radiusX = topLeft.X - rect.TopLeft.X;
        radiusY = leftTop.Y - rect.TopLeft.Y;
        if (!_IsZero(radiusX)
            || !_IsZero(radiusY))
        {
            ctx.ArcTo(topLeft, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }
    }

    internal static Geometry? CalculateLayoutClip(Size layoutSlotSize, Thickness borderThickness, CornerRadius cornerRadius)
    {
        if (layoutSlotSize.Width <= 0 ||
            layoutSlotSize.Height <= 0)
        {
            return new RectangleGeometry(new Rect(0, 0, 0, 0));
        }

        var rect = new Rect(0, 0, layoutSlotSize.Width, layoutSlotSize.Height);
        var radii = new Radii(cornerRadius, borderThickness, true);

        var layoutGeometry = new StreamGeometry();
        using StreamGeometryContext ctx = layoutGeometry.Open();
        GenerateGeometry(ctx, rect, radii);

        layoutGeometry.Freeze();
        return layoutGeometry;
    }

    internal struct Radii
    {
        internal Radii(CornerRadius radii, Thickness borders, bool outer)
        {
            double left = 0.5 * borders.Left;
            double top = 0.5 * borders.Top;
            double right = 0.5 * borders.Right;
            double bottom = 0.5 * borders.Bottom;

            if (outer)
            {
                if (_IsZero(radii.TopLeft))
                {
                    LeftTop = TopLeft = 0.0;
                }
                else
                {
                    LeftTop = radii.TopLeft + left;
                    TopLeft = radii.TopLeft + top;
                }
                if (_IsZero(radii.TopRight))
                {
                    TopRight = RightTop = 0.0;
                }
                else
                {
                    TopRight = radii.TopRight + top;
                    RightTop = radii.TopRight + right;
                }
                if (_IsZero(radii.BottomRight))
                {
                    RightBottom = BottomRight = 0.0;
                }
                else
                {
                    RightBottom = radii.BottomRight + right;
                    BottomRight = radii.BottomRight + bottom;
                }
                if (_IsZero(radii.BottomLeft))
                {
                    BottomLeft = LeftBottom = 0.0;
                }
                else
                {
                    BottomLeft = radii.BottomLeft + bottom;
                    LeftBottom = radii.BottomLeft + left;
                }
            }
            else
            {
                LeftTop = Math.Max(0.0, radii.TopLeft - left);
                TopLeft = Math.Max(0.0, radii.TopLeft - top);
                TopRight = Math.Max(0.0, radii.TopRight - top);
                RightTop = Math.Max(0.0, radii.TopRight - right);
                RightBottom = Math.Max(0.0, radii.BottomRight - right);
                BottomRight = Math.Max(0.0, radii.BottomRight - bottom);
                BottomLeft = Math.Max(0.0, radii.BottomLeft - bottom);
                LeftBottom = Math.Max(0.0, radii.BottomLeft - left);
            }
        }

        internal double LeftTop;
        internal double TopLeft;
        internal double TopRight;
        internal double RightTop;
        internal double RightBottom;
        internal double BottomRight;
        internal double BottomLeft;
        internal double LeftBottom;
    }
}
