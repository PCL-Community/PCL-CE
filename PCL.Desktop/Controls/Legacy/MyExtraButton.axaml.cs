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

public partial class MyExtraButton : Grid
{
    public delegate bool ShowCheckHandler();

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
    private readonly Grid? _iconHost;
    private readonly PathShape? _path;
    private readonly SvgIcon? _svgIcon;
    private bool _leftPressed;
    private bool _rightPressed;

    public ShowCheckHandler? ShowCheck { get; set; }

    public MyExtraButton()
    {
        AvaloniaXamlLoader.Load(this);
        _panClick = this.FindControl<Border>("PanClick");
        _panColor = this.FindControl<Border>("PanColor");
        _panProgress = this.FindControl<Border>("PanProgress");
        _iconHost = this.FindControl<Grid>("IconHost");
        _path = this.FindControl<PathShape>("Path");
        _svgIcon = this.FindControl<SvgIcon>("ShapeSvgIcon");

        PointerEntered += (_, _) => RefreshVisual();
        PointerExited += (_, _) =>
        {
            _leftPressed = false;
            _rightPressed = false;
            RefreshVisual();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;

        this.GetObservable(LogoProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(LogoScaleProperty).Subscribe(_ => RefreshScale());
        this.GetObservable(ProgressProperty).Subscribe(_ => RefreshProgress());
        this.GetObservable(ShowProperty).Subscribe(value =>
        {
            IsVisible = value;
            Height = value ? 50 : 0;
        });

        RefreshIcon();
        RefreshScale();
        RefreshProgress();
        RefreshVisual();
    }

    public event EventHandler? Click;
    public event EventHandler<PointerReleasedEventArgs>? RightClick;

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
        if (ShowCheck is not null)
            Show = ShowCheck();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed && CanRightClick)
            _rightPressed = true;
        else if (point.Properties.IsLeftButtonPressed)
            _leftPressed = true;
        else
            return;

        Focus();
        RefreshVisual();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_rightPressed && CanRightClick)
        {
            _rightPressed = false;
            RightClick?.Invoke(this, e);
            e.Handled = true;
        }
        else if (_leftPressed)
        {
            _leftPressed = false;
            Click?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        RefreshVisual();
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
        RefreshVisual();
    }

    private void RefreshScale()
    {
        if (_iconHost is not null)
            _iconHost.RenderTransform = new ScaleTransform(LogoScale, LogoScale);
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

    private void RefreshVisual()
    {
        var accent = Color.Parse("#1370f3");
        if (_panColor is not null)
            _panColor.Background = new SolidColorBrush(accent);
        if (_path is not null)
        {
            _path.Fill = new SolidColorBrush(Color.Parse("#eaf2fe"));
            _path.Stroke = _path.Fill;
        }
        if (_svgIcon is not null)
            _svgIcon.IconBrush = new SolidColorBrush(Color.Parse("#eaf2fe"));
        if (_panClick is not null)
        {
            var alpha = _leftPressed || _rightPressed ? 0x20 : IsPointerOver ? 0x12 : 0x00;
            _panClick.Background = new SolidColorBrush(Color.FromArgb((byte)alpha, accent.R, accent.G, accent.B));
        }
    }
}
