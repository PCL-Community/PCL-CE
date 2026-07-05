// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using PathShape = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SvgIconControl = PCL.Desktop.Controls.Legacy.SvgIcon;

namespace PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1051, CA1708
public partial class MyExtraButton : Grid
{
    public delegate bool ShowCheckHandler();

    private const int ColorAnimationInMilliseconds = 120;
    private const int ColorAnimationOutMilliseconds = 150;

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyExtraButton, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyExtraButton, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyExtraButton, double>(nameof(LogoScale), 1d);

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<MyExtraButton, double>(nameof(Progress));

    public static readonly StyledProperty<bool> ShowProperty =
        AvaloniaProperty.Register<MyExtraButton, bool>(nameof(Show));

    public static readonly StyledProperty<bool> CanRightClickProperty =
        AvaloniaProperty.Register<MyExtraButton, bool>(nameof(CanRightClick));

    public static readonly StyledProperty<object?> ToolTipProperty =
        AvaloniaProperty.Register<MyExtraButton, object?>(nameof(ToolTip));

    private readonly Border? _panClick;
    private readonly Border? _panColor;
    private readonly Border? _panProgress;
    private readonly Grid? _panScale;
    private readonly Grid? _iconHost;
    private readonly PathShape? _path;
    private readonly SvgIcon? _svgIcon;
    private bool _isLoaded;
    private bool _leftPressed;
    private bool _rightPressed;

    public ShowCheckHandler? showCheck;

    public ShowCheckHandler? ShowCheck
    {
        get => showCheck;
        set => showCheck = value;
    }

    public MyExtraButton()
    {
        AvaloniaXamlLoader.Load(this);
        _panClick = this.FindControl<Border>("PanClick");
        _panColor = this.FindControl<Border>("PanColor");
        _panProgress = this.FindControl<Border>("PanProgress");
        _panScale = this.FindControl<Grid>("PanScale");
        _iconHost = this.FindControl<Grid>("IconHost");
        _path = this.FindControl<PathShape>("Path");
        _svgIcon = this.FindControl<SvgIcon>("ShapeSvgIcon");

        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) =>
        {
            ButtonMouseLeave();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        AttachedToVisualTree += (_, _) =>
        {
            _isLoaded = true;
            ApplyShowState(Show, animate: false);
            RefreshColor();
        };

        this.GetObservable(LogoProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(LogoScaleProperty).Subscribe(_ => RefreshScale());
        this.GetObservable(ProgressProperty).Subscribe(_ => RefreshProgress());
        this.GetObservable(ShowProperty).Subscribe(value => ApplyShowState(value, _isLoaded));
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());

        RefreshIcon();
        RefreshScale();
        RefreshProgress();
        ApplyShowState(Show, animate: false);
        RefreshColor();
    }

    public event EventHandler? Click;
    public event EventHandler<PointerReleasedEventArgs>? RightClick;

    public int Uuid { get; } = Random.Shared.Next();

    public string Logo
    {
        get => GetValue(LogoProperty);
        set => SetValue(LogoProperty, value);
    }

    public string SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    public double LogoScale
    {
        get => GetValue(LogoScaleProperty);
        set => SetValue(LogoScaleProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool Show
    {
        get => GetValue(ShowProperty);
        set => SetValue(ShowProperty, value);
    }

    public bool CanRightClick
    {
        get => GetValue(CanRightClickProperty);
        set => SetValue(CanRightClickProperty, value);
    }

    public object? ToolTip
    {
        get => GetValue(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
    }

    public void ShowRefresh()
    {
        if (showCheck is not null)
            Show = showCheck();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            if (!CanRightClick)
                return;
            if (!_leftPressed && !_rightPressed)
                StartScaleAnimation(0.85d, -0.05d);
            _rightPressed = true;
        }
        else if (point.Properties.IsLeftButtonPressed)
        {
            if (!_leftPressed && !_rightPressed)
                StartScaleAnimation(0.85d, -0.05d);
            _leftPressed = true;
        }
        else
        {
            return;
        }

        Focus();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_rightPressed && CanRightClick)
        {
            _rightPressed = false;
            RightClick?.Invoke(this, e);
            ButtonRightMouseUp();
            e.Handled = true;
        }
        else if (_leftPressed)
        {
            _leftPressed = false;
            Click?.Invoke(this, EventArgs.Empty);
            ButtonLeftMouseUp();
            e.Handled = true;
        }
        RefreshColor();
    }

    private void ButtonLeftMouseUp()
    {
        if (!_rightPressed)
            RefreshScaleAfterRelease();
        RefreshColor();
    }

    private void ButtonRightMouseUp()
    {
        if (!CanRightClick)
            return;
        if (!_leftPressed)
            RefreshScaleAfterRelease();
        RefreshColor();
    }

    private void ButtonMouseLeave()
    {
        _leftPressed = false;
        _rightPressed = false;
        if (_panScale is not null)
        {
            ModAnimation.AniStart(
                ModAnimation.AaScaleTransform(
                    _panScale,
                    1d - GetScaleX(_panScale),
                    500,
                    ease: new ModAnimation.AniEaseOutFluent()),
                "MyExtraButton Scale " + Uuid);
        }
        RefreshColor();
    }

    private void RefreshIcon()
    {
        if (_path is null || _svgIcon is null)
            return;

        var usesSvg = !string.IsNullOrWhiteSpace(SvgIcon);
        _path.IsVisible = !usesSvg;
        _svgIcon.IsVisible = usesSvg;
        if (usesSvg)
        {
            _svgIcon.Icon = SvgIcon;
        }
        else if (!string.IsNullOrWhiteSpace(Logo))
        {
            try
            {
                _path.Data = Geometry.Parse(Logo);
            }
            catch (FormatException)
            {
                _path.Data = null;
            }
        }
        else
        {
            _path.Data = null;
        }
        RefreshScale();
        RefreshColor();
    }

    private void RefreshScale()
    {
        if (_iconHost is not null)
        {
            double scale = string.IsNullOrWhiteSpace(SvgIcon) ? LogoScale : 1d;
            _iconHost.RenderTransform = new ScaleTransform(scale, scale);
        }
    }

    private void RefreshProgress()
    {
        if (_panProgress is null)
            return;

        var value = Math.Clamp(Progress, 0d, 1d);
        _panProgress.IsVisible = value > 0.0001d;
        _panProgress.Clip = new RectangleGeometry
        {
            Rect = new Rect(0d, 40d * (1d - value), 40d, 40d * value)
        };
    }

    private void ApplyShowState(bool show, bool animate)
    {
        IsHitTestVisible = show;
        if (!animate)
        {
            IsVisible = show;
            Height = show ? 50d : 0d;
            SetScale(this, show ? 1d : 0d);
            return;
        }

        if (show)
        {
            IsVisible = true;
            ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaScaleTransform(
                    this,
                    0.3d - GetScaleX(this),
                    500,
                    60,
                    new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaScaleTransform(
                    this,
                    0.7d,
                    500,
                    60,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaHeight(
                    this,
                    50d - Height,
                    200,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
            }, "MyExtraButton MainScale " + Uuid);
            return;
        }

        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaScaleTransform(
                this,
                -GetScaleX(this),
                100,
                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaHeight(this, -Height, 400, 100, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() => IsVisible = false, after: true)
        }, "MyExtraButton MainScale " + Uuid);
    }

    private void StartScaleAnimation(double targetScale, double reboundScale, int reboundDuration = 60)
    {
        if (_panScale is null)
            return;

        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaScaleTransform(
                _panScale,
                targetScale - GetScaleX(_panScale),
                800,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
            ModAnimation.AaScaleTransform(
                _panScale,
                reboundScale,
                reboundDuration,
                ease: new ModAnimation.AniEaseOutFluent())
        }, "MyExtraButton Scale " + Uuid);
    }

    private void RefreshScaleAfterRelease()
    {
        if (_panScale is null)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(_panScale, 1d - GetScaleX(_panScale), 300, ease: new ModAnimation.AniEaseOutBack()),
            "MyExtraButton Scale " + Uuid);
    }

    public void RefreshColor()
    {
        if (_panColor is null)
            return;

        string colorKey = !IsEnabled
            ? "ColorBrushGray4"
            : IsPointerOver ? "ColorBrush4" : "ColorBrush3";
        int duration = !IsEnabled || IsPointerOver ? ColorAnimationInMilliseconds : ColorAnimationOutMilliseconds;

        if (_isLoaded)
        {
            ModAnimation.AniStart(
                ModAnimation.AaColor(_panColor, Border.BackgroundProperty, colorKey, duration),
                "MyExtraButton Color " + Uuid);
        }
        else
        {
            _panColor.Background = FindBrush(colorKey, "#1370f3");
        }

        IBrush iconBrush = FindBrush("ColorBrush8", "#eaf2fe");
        if (_path is not null)
        {
            _path.Fill = iconBrush;
            _path.Stroke = iconBrush;
        }
        if (_svgIcon is not null)
            _svgIcon.IconBrush = iconBrush;
        if (_panClick is not null)
            _panClick.Background = FindBrush("ColorBrushSemiTransparent", "#01eaf2fe");
    }

    public void Ribble()
    {
        if (_panScale is null)
            return;

        Border shape = new()
        {
            CornerRadius = new CornerRadius(1000d),
            BorderThickness = new Thickness(0.001d),
            Opacity = 0.5d,
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform(),
            Background = FindBrush("ColorBrush5", "#96c0f9")
        };
        _panScale.Children.Insert(0, shape);
        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaScaleTransform(
                shape,
                13d,
                1000,
                ease: new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Strong, 0.3d)),
            ModAnimation.AaOpacity(shape, -shape.Opacity, 1000),
            ModAnimation.AaCode(() => _panScale.Children.Remove(shape), after: true)
        }, "ExtraButton Ribble " + Random.Shared.Next());
    }

    private static double GetScaleX(Control control) =>
        control.RenderTransform switch
        {
            ScaleTransform scale => scale.ScaleX,
            TransformGroup group => group.Children.OfType<ScaleTransform>().FirstOrDefault()?.ScaleX ?? 1d,
            _ => 1d
        };

    private static void SetScale(Control control, double scale)
    {
        ControlVisualHelpers.SetCenterScale(control, scale);
    }

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }
}
#pragma warning restore CA1051, CA1708
