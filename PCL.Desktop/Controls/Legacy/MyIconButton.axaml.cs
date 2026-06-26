// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using PathShape = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public enum MyIconButtonTheme
{
    Color,
    White,
    Black,
    Red,
    Custom
}

public partial class MyIconButton : Border
{
    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyIconButton, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyIconButton, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyIconButton, double>(nameof(LogoScale), 1d);

    public new static readonly StyledProperty<MyIconButtonTheme> ThemeProperty =
        AvaloniaProperty.Register<MyIconButton, MyIconButtonTheme>(nameof(Theme));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MyIconButton, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyIconButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyIconButton, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<bool> IsScaleAnimationEnabledProperty =
        AvaloniaProperty.Register<MyIconButton, bool>(nameof(IsScaleAnimationEnabled), true);

    public static readonly StyledProperty<object?> ToolTipProperty =
        AvaloniaProperty.Register<MyIconButton, object?>(nameof(ToolTip));

    private readonly Border? _back;
    private readonly Grid? _iconHost;
    private readonly PathShape? _path;
    private readonly SvgIcon? _svgIcon;
    private bool _isPressed;

    public MyIconButton()
    {
        AvaloniaXamlLoader.Load(this);
        _back = this.FindControl<Border>("PanBack");
        _iconHost = this.FindControl<Grid>("IconHost");
        _path = this.FindControl<PathShape>("Path");
        _svgIcon = this.FindControl<SvgIcon>("ShapeSvgIcon");

        PointerEntered += (_, _) => RefreshVisual();
        PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshVisual();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;

        this.GetObservable(LogoProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(LogoScaleProperty).Subscribe(_ => RefreshScale());
        this.GetObservable(ThemeProperty).Subscribe(_ => RefreshVisual());
        this.GetObservable(ForegroundProperty).Subscribe(_ => RefreshVisual());

        RefreshIcon();
        RefreshScale();
        RefreshVisual();
    }

    public event EventHandler? Click;

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

    public new MyIconButtonTheme Theme
    {
        get => GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public bool IsScaleAnimationEnabled
    {
        get => GetValue(IsScaleAnimationEnabledProperty);
        set => SetValue(IsScaleAnimationEnabledProperty, value);
    }

    public object? ToolTip
    {
        get => GetValue(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var measured = base.MeasureOverride(availableSize);
        if (double.IsNaN(Width) && !double.IsNaN(Height) && Height > 0 && !double.IsInfinity(Height))
            return new Size(Height, Height);
        return measured;
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
        var parameter = CommandParameter;
        if (Command?.CanExecute(parameter) == true)
            Command.Execute(parameter);
        Click?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void RefreshIcon()
    {
        if (_path is null || _svgIcon is null)
            return;

        var svgIcon = SvgIcon;
        var usesSvg = !string.IsNullOrWhiteSpace(svgIcon);
        _path.IsVisible = !usesSvg;
        _svgIcon.IsVisible = usesSvg;
        if (usesSvg)
        {
            _svgIcon.Icon = svgIcon;
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

    private void RefreshVisual()
    {
        var color = Theme switch
        {
            MyIconButtonTheme.White => Colors.White,
            MyIconButtonTheme.Black => Colors.Black,
            MyIconButtonTheme.Red => Color.Parse("#ce2111"),
            MyIconButtonTheme.Custom when Foreground is SolidColorBrush customBrush => customBrush.Color,
            _ => Color.Parse("#1370f3")
        };

        var brush = new SolidColorBrush(color);
        if (_path is not null)
        {
            _path.Fill = brush;
            _path.Stroke = brush;
        }
        if (_svgIcon is not null)
            _svgIcon.IconBrush = brush;
        if (_back is not null)
        {
            var alpha = _isPressed ? 0x28 : IsPointerOver ? 0x18 : 0x00;
            _back.Background = new SolidColorBrush(Color.FromArgb((byte)alpha, color.R, color.G, color.B));
        }
    }
}
