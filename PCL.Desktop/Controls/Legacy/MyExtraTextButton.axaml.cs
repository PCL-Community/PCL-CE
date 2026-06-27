// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

public sealed partial class MyExtraTextButton : Grid
{
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

        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(LogoProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(LogoScaleProperty).Subscribe(_ => ApplyLogoScale());
        this.GetObservable(ShowProperty).Subscribe(ApplyShowState);
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());

        RefreshIcon();
        ApplyLogoScale();
        ApplyShowState(Show);
        RefreshColor();
    }

    public event EventHandler? Click;

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
        ApplyScale(0.85d);
        RefreshColor();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        ApplyScale(1d);
        RefreshColor();
        Click?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isPressed = false;
        ApplyScale(1d);
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

    private void ApplyLogoScale()
    {
        if (_iconHost is not null)
            _iconHost.RenderTransform = new ScaleTransform(LogoScale, LogoScale);
    }

    private void ApplyShowState(bool show)
    {
        Opacity = show ? 1d : 0d;
        IsHitTestVisible = show;
        if (RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = show ? 1d : 0d;
            scale.ScaleY = show ? 1d : 0d;
        }
    }

    private void ApplyScale(double scale)
    {
        if (_scaleLayer is not null)
            _scaleLayer.RenderTransform = new ScaleTransform(scale, scale);
    }

    private void RefreshColor()
    {
        if (_colorLayer is null)
            return;

        Color color = !IsEnabled
            ? Color.Parse("#8c8c8c")
            : _isPressed
                ? Color.Parse("#0f5fd0")
                : IsPointerOver
                    ? Color.Parse("#4092f7")
                    : Color.Parse("#1370f3");
        _colorLayer.Background = new SolidColorBrush(color);
    }
}
