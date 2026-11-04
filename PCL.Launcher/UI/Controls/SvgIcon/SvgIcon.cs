using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.Logging;
using PCL.Core.UI.Animation;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Core;
using PCL.Core.UI.Animation.Easings;
using PCL.UI.Controls.Common;

namespace PCL.UI.Controls.SvgIcon;

/// <summary>
///     按名称渲染 SVG 图标：先按 <see cref="Icon" /> 与 <see cref="DefaultPack" /> 加载模型，再按 <see cref="Stretch" />
///     缩放绘制。
///     图标资源位于 <c>avares://PCL.Launcher/Assets/IconPacks/{pack}/{name}.svg</c>。
/// </summary>
public class SvgIcon : Control
{
    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<SvgIcon, string>(nameof(Icon), string.Empty);

    public static readonly StyledProperty<string> DefaultPackProperty =
        AvaloniaProperty.Register<SvgIcon, string>(
            nameof(DefaultPack),
            SvgIconLoader.DefaultIconPack);

    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<SvgIcon, IBrush?>(nameof(IconBrush));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<SvgIcon, double>(
            nameof(StrokeThickness),
            2D,
            validate: value => !double.IsNaN(value) && value >= 0D);

    public static readonly StyledProperty<bool> UseOriginalColorProperty =
        AvaloniaProperty.Register<SvgIcon, bool>(nameof(UseOriginalColor));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<SvgIcon, Stretch>(nameof(Stretch), Stretch.Uniform);

    private string? _colorAnimationName;

    private long _colorAnimationStartStamp;

    private double _colorAnimationTargetMilliseconds;

    private SvgIconModel? _model;
    private bool _modelLoaded;

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string DefaultPack
    {
        get => GetValue(DefaultPackProperty);
        set => SetValue(DefaultPackProperty, value);
    }

    /// <summary>
    ///     未显式设置时使用页面文本色（<c>ColorBrush1</c>），找不到资源时回退为黑色。
    /// </summary>
    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool UseOriginalColor
    {
        get => GetValue(UseOriginalColorProperty);
        set => SetValue(UseOriginalColorProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    ///     把图标颜色过渡到目标颜色，默认耗时 120 ms，缓动为 CubicEaseOut。
    ///     同名动画开始前先经 <see cref="AnimationService.CancelAnimationByName" /> 取消旧动画，
    ///     过渡结束会写一条带目标时长与实测时长的 Debug 日志。
    /// </summary>
    public void AnimateIconBrushTo(
        MyColor color,
        TimeSpan? duration = null,
        object? easing = null,
        string? animationKey = null)
    {
        var name = animationKey ?? $"SvgIconColor {RuntimeHelpers.GetHashCode(this)}";
        var targetDuration = duration ?? TimeSpan.FromMilliseconds(120);

        AnimationService.CancelAnimationByName(name);

        _colorAnimationName = name;
        _colorAnimationStartStamp = Stopwatch.GetTimestamp();
        _colorAnimationTargetMilliseconds = targetDuration.TotalMilliseconds;

        IAnimation? running = null;
        var animation = new NColorFromToAnimation
        {
            Name = name,
            To = (Color)color,
            Duration = targetDuration,
            Easing = easing as IEasing ?? new CubicEaseOut()
        };
        animation.Completed += (_, _) => _OnColorAnimationCompleted(name, running);
        running = animation.RunFireAndForget(new AvaloniaAnimatable(this, IconBrushProperty));
    }

    public void StopColorAnimation()
    {
        var name = _colorAnimationName;
        if (name is null) return;

        _colorAnimationName = null;
        AnimationService.CancelAnimationByName(name);
    }

    private void _OnColorAnimationCompleted(string name, IAnimation? animation)
    {
        if (!string.Equals(_colorAnimationName, name, StringComparison.Ordinal)) return;

        var elapsed = Stopwatch.GetElapsedTime(_colorAnimationStartStamp).TotalMilliseconds;
        var status = animation?.Status;
        _colorAnimationName = null;
        LogWrapper.Debug(
            "Animation",
            $"[SvgIcon] 颜色过渡结束 目标={_colorAnimationTargetMilliseconds:F0}ms "
            + $"实测={elapsed:F1}ms 状态={status}");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var model = _GetModel();
        var naturalSize = model is null
            ? new Size(24D, 24D)
            : new Size(model.Width, model.Height);

        if (double.IsInfinity(availableSize.Width) && double.IsInfinity(availableSize.Height))
            return naturalSize;

        if (double.IsInfinity(availableSize.Width))
            return new Size(
                naturalSize.Width * availableSize.Height / naturalSize.Height,
                availableSize.Height);

        if (double.IsInfinity(availableSize.Height))
            return new Size(
                availableSize.Width,
                naturalSize.Height * availableSize.Width / naturalSize.Width);

        return availableSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var model = _GetModel();
        var renderSize = Bounds.Size;
        if (model is null
            || model.Elements.Count == 0
            || renderSize.Width <= 0D
            || renderSize.Height <= 0D)
            return;

        var target = _CalculateTargetRect(new Size(model.Width, model.Height), renderSize, Stretch);
        if (target.Width <= 0D || target.Height <= 0D)
            return;

        var scaleX = target.Width / model.Width;
        var scaleY = target.Height / model.Height;

        var options = new SvgIconPaintOptions(
            IconBrush ?? _GetFallbackIconBrush(),
            StrokeThickness,
            UseOriginalColor);

        using (context.PushTransform(Matrix.CreateTranslation(target.X, target.Y)))
        using (context.PushTransform(Matrix.CreateScale(scaleX, scaleY)))
        using (context.PushTransform(Matrix.CreateTranslation(-model.MinX, -model.MinY)))
        {
            foreach (var element in model.Elements)
                element.Draw(context, options);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty || change.Property == DefaultPackProperty)
        {
            _model = null;
            _modelLoaded = false;
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == StretchProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == IconBrushProperty
                 || change.Property == StrokeThicknessProperty
                 || change.Property == UseOriginalColorProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += _OnActualThemeVariantChanged;
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= _OnActualThemeVariantChanged;
        StopColorAnimation();
        base.OnDetachedFromVisualTree(e);
    }

    private void _OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    private IBrush _GetFallbackIconBrush()
    {
        if (this.TryFindResource("ColorBrush1", out var resource) && resource is IBrush brush)
            return brush;

        if (this.TryFindResource("ColorBrush1", out var fallback)
            && fallback is IBrush fallbackBrush)
            return fallbackBrush;

        return Brushes.Black;
    }

    private SvgIconModel? _GetModel()
    {
        if (_modelLoaded)
            return _model;

        _model = SvgIconLoader.Load(Icon, DefaultPack);
        _modelLoaded = true;
        return _model;
    }

    private static Rect _CalculateTargetRect(Size sourceSize, Size renderSize, Stretch stretch)
    {
        if (sourceSize.Width <= 0D || sourceSize.Height <= 0D)
            sourceSize = new Size(24D, 24D);

        if (stretch == Stretch.None)
        {
            var x = (renderSize.Width - sourceSize.Width) / 2D;
            var y = (renderSize.Height - sourceSize.Height) / 2D;
            return new Rect(x, y, sourceSize.Width, sourceSize.Height);
        }

        var scaleX = renderSize.Width / sourceSize.Width;
        var scaleY = renderSize.Height / sourceSize.Height;

        var scale = stretch switch
        {
            Stretch.Fill => double.NaN,
            Stretch.UniformToFill => Math.Max(scaleX, scaleY),
            _ => Math.Min(scaleX, scaleY)
        };

        var width = stretch == Stretch.Fill ? renderSize.Width : sourceSize.Width * scale;
        var height = stretch == Stretch.Fill ? renderSize.Height : sourceSize.Height * scale;
        var left = (renderSize.Width - width) / 2D;
        var top = (renderSize.Height - height) / 2D;

        return new Rect(left, top, width, height);
    }
}