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

namespace PCL.Desktop.Controls.Legacy;

public partial class MyIconTextButton : Border
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyIconTextButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyIconTextButton, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyIconTextButton, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyIconTextButton, double>(nameof(LogoScale), 1d);

    private readonly TextBlock? _label;
    private readonly Grid? _logoHost;
    private readonly PathShape? _path;
    private readonly SvgIcon? _svgIcon;
    private bool _isPressed;

    public MyIconTextButton()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _logoHost = this.FindControl<Grid>("LogoHost");
        _path = this.FindControl<PathShape>("ShapeLogo");
        _svgIcon = this.FindControl<SvgIcon>("ShapeSvgIcon");

        PointerEntered += (_, _) => RefreshVisual();
        PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshVisual();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;

        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(LogoProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(LogoScaleProperty).Subscribe(_ => RefreshScale());

        RefreshIcon();
        RefreshScale();
        RefreshVisual();
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

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isPressed = true;
        Focus();
        RefreshVisual();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        RefreshVisual();
        Click?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
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
    }

    private void RefreshScale()
    {
        if (_logoHost is not null)
            _logoHost.RenderTransform = new ScaleTransform(LogoScale, LogoScale);
    }

    private void RefreshVisual()
    {
        var color = _isPressed ? Color.Parse("#d5e6fd") : IsPointerOver ? Color.Parse("#e0eafd") : Colors.Transparent;
        Background = new SolidColorBrush(color);
    }
}
