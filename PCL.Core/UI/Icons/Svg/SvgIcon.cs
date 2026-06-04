using System;
using System.Windows;
using System.Windows.Media;
using PCL.Core.UI.Animation;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Core;
using PCL.Core.UI.Animation.Easings;

namespace PCL.Core.UI.Icons.Svg;

public class SvgIcon : FrameworkElement
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(string),
        typeof(SvgIcon),
        new FrameworkPropertyMetadata(string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnIconChanged));

    public static readonly DependencyProperty DefaultPackProperty = DependencyProperty.Register(
        nameof(DefaultPack),
        typeof(string),
        typeof(SvgIcon),
        new FrameworkPropertyMetadata(SvgIconLoader.DefaultIconPack,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnIconChanged));

    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush),
        typeof(Brush),
        typeof(SvgIcon),
        new FrameworkPropertyMetadata(SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(SvgIcon),
        new FrameworkPropertyMetadata(2D, FrameworkPropertyMetadataOptions.AffectsRender),
        value => value is double number && !double.IsNaN(number) && number >= 0D);

    public static readonly DependencyProperty UseOriginalColorProperty = DependencyProperty.Register(
        nameof(UseOriginalColor),
        typeof(bool),
        typeof(SvgIcon),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(SvgIcon),
        new FrameworkPropertyMetadata(Stretch.Uniform,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    private SvgIconModel? _model;
    private bool _modelLoaded;

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string DefaultPack
    {
        get => (string)GetValue(DefaultPackProperty);
        set => SetValue(DefaultPackProperty, value);
    }

    public Brush IconBrush
    {
        get => (Brush)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool UseOriginalColor
    {
        get => (bool)GetValue(UseOriginalColorProperty);
        set => SetValue(UseOriginalColorProperty, value);
    }

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public IAnimation AnimateIconBrushTo(NColor color, TimeSpan? duration = null, IEasing? easing = null)
    {
        EnsureAnimatableIconBrush();

        var animation = new NColorFromToAnimation
        {
            To = color,
            Duration = duration ?? TimeSpan.FromMilliseconds(120),
            Easing = easing ?? new CubicEaseOut()
        };

        return animation.RunFireAndForget(new WpfAnimatable(this, IconBrushProperty));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var model = GetModel();
        var naturalSize = model is null
            ? new Size(24D, 24D)
            : new Size(model.Width, model.Height);

        if (double.IsInfinity(availableSize.Width) && double.IsInfinity(availableSize.Height))
            return naturalSize;

        if (double.IsInfinity(availableSize.Width))
            return new Size(naturalSize.Width * availableSize.Height / naturalSize.Height, availableSize.Height);

        if (double.IsInfinity(availableSize.Height))
            return new Size(availableSize.Width, naturalSize.Height * availableSize.Width / naturalSize.Width);

        return availableSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var model = GetModel();
        if (model is null || model.Elements.Count == 0 || RenderSize.Width <= 0D || RenderSize.Height <= 0D)
            return;

        var target = CalculateTargetRect(model, RenderSize, Stretch);
        if (target.Width <= 0D || target.Height <= 0D)
            return;

        var scaleX = target.Width / model.Width;
        var scaleY = target.Height / model.Height;

        drawingContext.PushTransform(new TranslateTransform(target.X, target.Y));
        drawingContext.PushTransform(new ScaleTransform(scaleX, scaleY));
        drawingContext.PushTransform(new TranslateTransform(-model.MinX, -model.MinY));

        var options = new SvgIconPaintOptions(IconBrush, StrokeThickness, UseOriginalColor);
        foreach (var element in model.Elements)
            element.Draw(drawingContext, options);

        drawingContext.Pop();
        drawingContext.Pop();
        drawingContext.Pop();
    }

    private SvgIconModel? GetModel()
    {
        if (_modelLoaded)
            return _model;

        _model = SvgIconLoader.Load(Icon, DefaultPack);
        _modelLoaded = true;
        return _model;
    }

    private static void OnIconChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var icon = (SvgIcon)dependencyObject;
        icon._model = null;
        icon._modelLoaded = false;
    }

    private void EnsureAnimatableIconBrush()
    {
        if (IconBrush is SolidColorBrush { IsFrozen: false })
            return;

        IconBrush = IconBrush switch
        {
            SolidColorBrush solidColorBrush => new SolidColorBrush(solidColorBrush.Color),
            _ => new SolidColorBrush(Colors.Black)
        };
    }

    private static Rect CalculateTargetRect(SvgIconModel model, Size renderSize, Stretch stretch)
    {
        if (stretch == Stretch.None)
        {
            var x = (renderSize.Width - model.Width) / 2D;
            var y = (renderSize.Height - model.Height) / 2D;
            return new Rect(x, y, model.Width, model.Height);
        }

        var scaleX = renderSize.Width / model.Width;
        var scaleY = renderSize.Height / model.Height;

        var scale = stretch switch
        {
            Stretch.Fill => double.NaN,
            Stretch.UniformToFill => Math.Max(scaleX, scaleY),
            _ => Math.Min(scaleX, scaleY)
        };

        var width = stretch == Stretch.Fill ? renderSize.Width : model.Width * scale;
        var height = stretch == Stretch.Fill ? renderSize.Height : model.Height * scale;
        var left = (renderSize.Width - width) / 2D;
        var top = (renderSize.Height - height) / 2D;

        return new Rect(left, top, width, height);
    }
}