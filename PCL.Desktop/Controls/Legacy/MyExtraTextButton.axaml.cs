// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

public sealed partial class MyExtraTextButton : Grid
{
    private const int ColorAnimationInMilliseconds = 120;
    private const int ColorAnimationOutMilliseconds = 150;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyExtraTextButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyExtraTextButton, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyExtraTextButton, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyExtraTextButton, double>(nameof(LogoScale), 1d);

    public static readonly StyledProperty<bool> ShowProperty =
        AvaloniaProperty.Register<MyExtraTextButton, bool>(nameof(Show));

    private readonly Border? _colorLayer;
    private readonly Border? _clickLayer;
    private readonly Grid? _scaleLayer;
    private readonly Grid? _iconHost;
    private readonly PathShape? _path;
    private readonly SvgIcon? _svgIcon;
    private readonly TextBlock? _label;
    private bool _isLoaded;
    private bool _isPressed;

    public MyExtraTextButton()
    {
        AvaloniaXamlLoader.Load(this);
        _colorLayer = this.FindControl<Border>("PanColor");
        _clickLayer = this.FindControl<Border>("PanClick");
        _scaleLayer = this.FindControl<Grid>("PanScale");
        _iconHost = this.FindControl<Grid>("IconHost");
        _path = this.FindControl<PathShape>("Path");
        _svgIcon = this.FindControl<SvgIcon>("ShapeSvgIcon");
        _label = this.FindControl<TextBlock>("LabText");

        if (_clickLayer is not null)
        {
            _clickLayer.PointerPressed += OnPointerPressed;
            _clickLayer.PointerReleased += OnPointerReleased;
            _clickLayer.PointerExited += OnPointerExited;
            _clickLayer.PointerEntered += (_, _) => RefreshColor();
        }
        SizeChanged += (_, _) => RefreshCornerRadius();
        AttachedToVisualTree += (_, _) =>
        {
            _isLoaded = true;
            ApplyShowState(Show, animate: false);
            RefreshCornerRadius();
            RefreshColor();
        };

        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(LogoProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(LogoScaleProperty).Subscribe(_ => ApplyLogoScale());
        this.GetObservable(ShowProperty).Subscribe(value => ApplyShowState(value, _isLoaded));
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());

        RefreshIcon();
        ApplyLogoScale();
        ApplyShowState(Show, animate: false);
        RefreshCornerRadius();
        RefreshColor();
    }

    public event EventHandler? Click;

    public int Uuid { get; } = Random.Shared.Next();

    public InlineCollection Inlines =>
        _label?.Inlines ?? throw new InvalidOperationException("MyExtraTextButton text block is not initialized.");

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

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

    public bool Show
    {
        get => GetValue(ShowProperty);
        set => SetValue(ShowProperty, value);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !Show || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isPressed = true;
        Focus();
        StartScaleAnimation(0.85d, -0.05d);
        RefreshColor();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        RefreshScaleAfterRelease();
        RefreshColor();
        Click?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isPressed = false;
        if (_scaleLayer is not null)
        {
            ModAnimation.AniStart(
                ModAnimation.AaScaleTransform(
                    _scaleLayer,
                    1d - GetScaleX(_scaleLayer),
                    500,
                    ease: new ModAnimation.AniEaseOutFluent()),
                "MyExtraTextButton Scale " + Uuid);
        }
        RefreshColor();
    }

    private void RefreshIcon()
    {
        if (_path is null || _svgIcon is null)
            return;

        bool usesSvg = !string.IsNullOrWhiteSpace(SvgIcon);
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

        RefreshIconHostVisibility();
    }

    private void RefreshIconHostVisibility()
    {
        if (_iconHost is null || _label is null)
            return;

        bool hasIcon = !string.IsNullOrWhiteSpace(Logo) || !string.IsNullOrWhiteSpace(SvgIcon);
        _iconHost.IsVisible = hasIcon;
        _iconHost.Width = hasIcon ? 16d : 0d;
        _iconHost.Margin = hasIcon ? new Thickness(2d, 12d, 0d, 12d) : new Thickness(0d, 12d, 0d, 12d);
        _label.Margin = hasIcon ? new Thickness(12d, 0d, 0d, 0.8d) : new Thickness(0d, 0d, 0d, 0.8d);
    }

    private void RefreshCornerRadius()
    {
        double height = Bounds.Height;
        if (height <= 0d && !double.IsNaN(Height) && !double.IsInfinity(Height))
            height = Height;

        CornerRadius cornerRadius = new(Math.Max(0d, height * 0.4d));
        if (_clickLayer is not null)
            _clickLayer.CornerRadius = cornerRadius;
        if (_colorLayer is not null)
            _colorLayer.CornerRadius = cornerRadius;
    }

    private void ApplyLogoScale()
    {
        if (_iconHost is not null)
        {
            double scale = string.IsNullOrWhiteSpace(SvgIcon) ? LogoScale : 1d;
            _iconHost.RenderTransform = new ScaleTransform(scale, scale);
        }
    }

    private void ApplyShowState(bool show, bool animate)
    {
        IsHitTestVisible = show;
        if (!animate)
        {
            IsVisible = show;
            Opacity = show ? 1d : 0d;
            SetScale(this, show ? 1d : 0d);
            return;
        }

        if (show)
        {
            IsVisible = true;
            Opacity = 0d;
            ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaOpacity(this, 1d - Opacity, 80, 50),
                ModAnimation.AaScaleTransform(
                    this,
                    0.15d - GetScaleX(this),
                    400,
                    50,
                    new ModAnimation.AniEaseOutBack()),
                ModAnimation.AaScaleTransform(
                    this,
                    0.85d,
                    160,
                    50,
                    new ModAnimation.AniEaseOutFluent())
            }, "MyExtraTextButton MainScale " + Uuid);
            return;
        }

        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaOpacity(this, -Opacity, 50, 50),
            ModAnimation.AaScaleTransform(
                this,
                -GetScaleX(this),
                100,
                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => IsVisible = false, after: true)
        }, "MyExtraTextButton MainScale " + Uuid);
    }

    private void StartScaleAnimation(double targetScale, double reboundScale, int reboundDuration = 60)
    {
        if (_scaleLayer is null)
            return;

        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaScaleTransform(
                _scaleLayer,
                targetScale - GetScaleX(_scaleLayer),
                800,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
            ModAnimation.AaScaleTransform(
                _scaleLayer,
                reboundScale,
                reboundDuration,
                ease: new ModAnimation.AniEaseOutFluent())
        }, "MyExtraTextButton Scale " + Uuid);
    }

    private void RefreshScaleAfterRelease()
    {
        if (_scaleLayer is null)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(_scaleLayer, 1d - GetScaleX(_scaleLayer), 300, ease: new ModAnimation.AniEaseOutBack()),
            "MyExtraTextButton Scale " + Uuid);
    }

    private void RefreshColor()
    {
        if (_colorLayer is null)
            return;

        string key = !IsEnabled
            ? "ColorBrushGray4"
            : IsPointerOver ? "ColorBrush4" : "ColorBrush3";
        int duration = !IsEnabled || IsPointerOver ? ColorAnimationInMilliseconds : ColorAnimationOutMilliseconds;

        if (_isLoaded)
        {
            ModAnimation.AniStart(
                ModAnimation.AaColor(_colorLayer, Border.BackgroundProperty, key, duration),
                "MyExtraTextButton Color " + Uuid);
        }
        else
        {
            _colorLayer.Background = FindBrush(key, "#1370f3");
        }
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
