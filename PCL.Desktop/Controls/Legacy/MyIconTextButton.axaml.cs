// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using PathShape = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SvgIconControl = PCL.Desktop.Controls.Legacy.SvgIcon;

namespace PCL.Desktop.Controls.Legacy;

public enum MyIconTextButtonColorState
{
    Black,
    Highlight
}

public partial class MyIconTextButton : Border
{
    public enum ColorState
    {
        Black,
        Highlight
    }

#pragma warning disable CA1711
    public delegate void ChangeEventHandler(object sender, bool raiseByMouse);

    public delegate void CheckEventHandler(object sender, bool raiseByMouse);

    public delegate void ClickEventHandler(object sender, IconTextButtonClickEventArgs e);
#pragma warning restore CA1711

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyIconTextButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyIconTextButton, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyIconTextButton, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyIconTextButton, double>(nameof(LogoScale), 1d);

    public static readonly StyledProperty<ColorState> ColorTypeProperty =
        AvaloniaProperty.Register<MyIconTextButton, ColorState>(nameof(ColorType));

    private readonly TextBlock? _label;
    private readonly Grid? _logoHost;
    private readonly PathShape? _path;
    private readonly SvgIcon? _svgIcon;
    private bool _hasLegacyLogo;
    private bool _isLoaded;
    private bool _isPressed;

    public MyIconTextButton()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _logoHost = this.FindControl<Grid>("LogoHost");
        _path = this.FindControl<PathShape>("ShapeLogo");
        _svgIcon = this.FindControl<SvgIcon>("ShapeSvgIcon");

        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) =>
        {
            MyIconTextButtonMouseLeave();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        AttachedToVisualTree += (_, _) =>
        {
            _isLoaded = true;
            RefreshColor();
        };

        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(LogoProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(LogoScaleProperty).Subscribe(_ => RefreshScale());
        this.GetObservable(ColorTypeProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());

        RefreshIcon();
        RefreshScale();
        RefreshColor();
    }

#pragma warning disable CS0067
    public event CheckEventHandler? Check;

    public event ChangeEventHandler? Change;
#pragma warning restore CS0067

    public event ClickEventHandler? Click;

    public int Uuid { get; } = Random.Shared.Next();

    public InlineCollection Inlines =>
        _label?.Inlines ?? throw new InvalidOperationException("MyIconTextButton text block is not initialized.");

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

    public ColorState ColorType
    {
        get => GetValue(ColorTypeProperty);
        set => SetValue(ColorTypeProperty, value);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isPressed = true;
        Focus();
        RefreshColor();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        Click?.Invoke(this, new IconTextButtonClickEventArgs(raiseByMouse: true));
        RefreshColor();
        e.Handled = true;
    }

    private void MyIconTextButtonMouseLeave()
    {
        _isPressed = false;
        RefreshColor();
    }

    private void RefreshIcon()
    {
        if (_path is null || _svgIcon is null)
            return;

        _hasLegacyLogo = !string.IsNullOrWhiteSpace(Logo);
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
        RefreshLogoHostVisibility();
        RefreshScale();
        RefreshColor();
    }

    private void RefreshScale()
    {
        if (_logoHost is not null)
        {
            double scale = string.IsNullOrWhiteSpace(SvgIcon) ? LogoScale : 1d;
            _logoHost.RenderTransform = new ScaleTransform(scale, scale);
        }
    }

    private void RefreshLogoHostVisibility()
    {
        if (_logoHost is null || _label is null)
            return;

        bool hasAnyIcon = !string.IsNullOrWhiteSpace(SvgIcon) || _hasLegacyLogo;
        _logoHost.IsVisible = hasAnyIcon;
        _logoHost.Width = hasAnyIcon ? 16d : 0d;
        _logoHost.Height = 16d;
        _logoHost.Margin = hasAnyIcon ? new Thickness(12d, 0d, 0d, 0d) : new Thickness();
        _label.Margin = hasAnyIcon ? new Thickness(7d, 0d, 12d, 1d) : new Thickness(12d, 0d, 12d, 1d);
    }

    private void RefreshColor()
    {
        if (!_isLoaded)
        {
            ApplyNonAnimatedColor();
            return;
        }

        if (_isPressed)
        {
            StartBackgroundAnimation("ColorBrush6", 70);
        }
        else if (IsPointerOver)
        {
            StartForegroundAnimation("ColorBrush3", 100);
            StartBackgroundAnimation("ColorBrushBg1", 100);
        }
        else if (IsEnabled)
        {
            StartForegroundAnimation(GetDefaultForegroundResourceKey(), 150);
            StartBackgroundAnimation("ColorBrushSemiTransparent", 150);
        }
        else
        {
            StartForegroundAnimation("ColorBrushGray5", 100);
            StartBackgroundAnimation("ColorBrushSemiTransparent", 150);
        }
    }

    private string GetDefaultForegroundResourceKey() =>
        ColorType == ColorState.Highlight ? "ColorBrush3" : "ColorBrush1";

    private void StartForegroundAnimation(string resourceKey, int duration)
    {
        List<ModAnimation.AniData> animations = [];
        if (_label is not null)
            animations.Add(ModAnimation.AaColor(_label, TextBlock.ForegroundProperty, resourceKey, duration));
        if (_svgIcon is not null && _svgIcon.IsVisible)
            animations.Add(ModAnimation.AaColor(_svgIcon, SvgIconControl.IconBrushProperty, resourceKey, duration));
        if (_path is not null && _path.IsVisible)
            animations.Add(ModAnimation.AaColor(_path, Shape.FillProperty, resourceKey, duration));

        ModAnimation.AniStart(animations, "MyIconTextButton Checked " + Uuid);
    }

    private void StartBackgroundAnimation(string resourceKey, int duration) =>
        ModAnimation.AniStart(
            ModAnimation.AaColor(this, Border.BackgroundProperty, resourceKey, duration),
            "MyIconTextButton Color " + Uuid);

    private void ApplyNonAnimatedColor()
    {
        ModAnimation.AniStop("MyIconTextButton Checked " + Uuid);
        ModAnimation.AniStop("MyIconTextButton Color " + Uuid);
        Background = FindBrush("ColorBrushSemiTransparent", "#01eaf2fe");
        IBrush foreground = FindBrush(IsEnabled ? GetDefaultForegroundResourceKey() : "ColorBrushGray5", IsEnabled ? "#343d4a" : "#cccccc");
        if (_label is not null)
            _label.Foreground = foreground;
        if (_path is not null)
            _path.Fill = foreground;
        if (_svgIcon is not null)
            _svgIcon.IconBrush = foreground;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }
}

#pragma warning disable CA1708
public sealed class IconTextButtonClickEventArgs(bool raiseByMouse = false) : EventArgs
{
    public bool Handled { get; set; }

    public bool handled
    {
        get => Handled;
        set => Handled = value;
    }

    public bool RaiseByMouse { get; } = raiseByMouse;

    public bool raiseByMouse => RaiseByMouse;
}
#pragma warning restore CA1708
