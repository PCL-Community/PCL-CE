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
using PCL.Desktop.Theme;
using SvgIconControl = PCL.Desktop.Controls.Legacy.SvgIcon;

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
    public enum Themes
    {
        Color,
        White,
        Black,
        Red,
        Custom
    }

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyIconButton, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyIconButton, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyIconButton, double>(nameof(LogoScale), 1d);

    public new static readonly StyledProperty<Themes> ThemeProperty =
        AvaloniaProperty.Register<MyIconButton, Themes>(nameof(Theme));

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
    private bool _isLoaded;
    private bool _isPressed;

    public MyIconButton()
    {
        AvaloniaXamlLoader.Load(this);
        _back = this.FindControl<Border>("PanBack");
        _iconHost = this.FindControl<Grid>("IconHost");
        _path = this.FindControl<PathShape>("Path");
        _svgIcon = this.FindControl<SvgIcon>("ShapeSvgIcon");

        PointerEntered += (_, _) => RefreshAnim();
        PointerExited += (_, _) =>
        {
            ButtonMouseLeave();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        AttachedToVisualTree += (_, _) =>
        {
            _isLoaded = true;
            RefreshAnim();
        };

        this.GetObservable(LogoProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(LogoScaleProperty).Subscribe(_ => RefreshScale());
        this.GetObservable(ThemeProperty).Subscribe(_ => RefreshAnim());
        this.GetObservable(ForegroundProperty).Subscribe(_ => RefreshAnim());
        this.GetObservable(ToolTipProperty).Subscribe(tip => Avalonia.Controls.ToolTip.SetTip(this, tip));

        RefreshIcon();
        RefreshScale();
        RefreshAnim();
    }

    public event EventHandler? Click;

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

    public new Themes Theme
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
        if (_back is not null)
        {
            ModAnimation.AniStart(
                ModAnimation.AaScaleTransform(
                    _back,
                    0.8d - GetScaleX(_back),
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                "MyIconButton Scale " + Uuid);
        }
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        var parameter = CommandParameter;
        if (Command?.CanExecute(parameter) == true)
            Command.Execute(parameter);
        Click?.Invoke(this, EventArgs.Empty);
        if (_back is not null)
        {
            ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaScaleTransform(
                    _back,
                    1.05d - GetScaleX(_back),
                    250,
                    ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaScaleTransform(
                    _back,
                    -0.05d,
                    250,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
            }, "MyIconButton Scale " + Uuid);
        }
        RefreshAnim();
        e.Handled = true;
    }

    private void ButtonMouseLeave()
    {
        _isPressed = false;
        if (_back is not null)
        {
            ModAnimation.AniStart(
                ModAnimation.AaScaleTransform(
                    _back,
                    1d - GetScaleX(_back),
                    250,
                    ease: new ModAnimation.AniEaseOutFluent()),
                "MyIconButton Scale " + Uuid);
        }
        RefreshAnim();
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
        RefreshAnim();
    }

    private void RefreshScale()
    {
        if (_iconHost is not null)
        {
            double scale = string.IsNullOrWhiteSpace(SvgIcon) ? LogoScale : 1d;
            _iconHost.RenderTransform = new ScaleTransform(scale, scale);
        }
    }

    public void RefreshAnim()
    {
        if (!_isLoaded)
        {
            ApplyNonAnimatedTheme();
            return;
        }

        ModAnimation.AniStart(IsPointerOver ? GetHoverAnimations() : GetNormalAnimations(), "MyIconButton Color " + Uuid);
    }

    private List<ModAnimation.AniData> GetHoverAnimations()
    {
        List<ModAnimation.AniData> animations = [];
        switch (Theme)
        {
            case Themes.Color:
                AddIconColorAnimation(animations, "ColorBrush2", 120);
                break;
            case Themes.White:
                AddBackColorAnimation(animations, Color.FromArgb(50, 255, 255, 255), 120);
                break;
            case Themes.Red:
                AddIconColorAnimation(animations, Color.FromRgb(255, 76, 76), 120);
                break;
            case Themes.Black:
                AddIconColorAnimation(animations, GetBlackThemeColor(alpha: 230), 120);
                break;
            case Themes.Custom:
                AddIconColorAnimation(animations, WithAlpha(GetForegroundColor(), 255), 120);
                break;
        }

        return animations;
    }

    private List<ModAnimation.AniData> GetNormalAnimations()
    {
        List<ModAnimation.AniData> animations = [];
        switch (Theme)
        {
            case Themes.Color:
                AddIconColorAnimation(animations, "ColorBrush4", 150);
                ClearBackImmediately();
                break;
            case Themes.White:
                AddIconColorAnimation(animations, Color.FromRgb(234, 242, 254), 150);
                AddBackColorAnimation(animations, Color.FromArgb(0, 255, 255, 255), 150);
                break;
            case Themes.Red:
                AddIconColorAnimation(animations, Color.FromArgb(160, 255, 76, 76), 150);
                ClearBackImmediately();
                break;
            case Themes.Black:
                AddIconColorAnimation(animations, GetBlackThemeColor(alpha: 160), 150);
                ClearBackImmediately();
                break;
            case Themes.Custom:
                AddIconColorAnimation(animations, WithAlpha(GetForegroundColor(), 160), 150);
                ClearBackImmediately();
                break;
        }

        return animations;
    }

    private void ApplyNonAnimatedTheme()
    {
        ModAnimation.AniStop("MyIconButton Color " + Uuid);
        Color icon = Theme switch
        {
            Themes.White => Colors.White,
            Themes.Black => GetBlackThemeColor(alpha: 160),
            Themes.Red => Color.FromArgb(160, 255, 76, 76),
            Themes.Custom => WithAlpha(GetForegroundColor(), 160),
            _ => FindColor("ColorBrush5", Color.Parse("#96c0f9"))
        };
        ApplyIconBrush(new SolidColorBrush(icon));
        ClearBackImmediately();
    }

    private void AddIconColorAnimation(List<ModAnimation.AniData> animations, string resourceKey, int duration)
    {
        if (_svgIcon is not null && _svgIcon.IsVisible)
            animations.Add(ModAnimation.AaColor(_svgIcon, SvgIconControl.IconBrushProperty, resourceKey, duration));
        if (_path is not null && _path.IsVisible)
        {
            animations.Add(ModAnimation.AaColor(_path, Shape.FillProperty, resourceKey, duration));
            animations.Add(ModAnimation.AaColor(_path, Shape.StrokeProperty, resourceKey, duration));
        }
    }

    private void AddIconColorAnimation(List<ModAnimation.AniData> animations, Color color, int duration)
    {
        if (_svgIcon is not null && _svgIcon.IsVisible)
            animations.Add(ModAnimation.AaColor(_svgIcon, SvgIconControl.IconBrushProperty, color, duration));
        if (_path is not null && _path.IsVisible)
        {
            animations.Add(ModAnimation.AaColor(_path, Shape.FillProperty, color, duration));
            animations.Add(ModAnimation.AaColor(_path, Shape.StrokeProperty, color, duration));
        }
    }

    private void AddBackColorAnimation(List<ModAnimation.AniData> animations, Color color, int duration)
    {
        if (_back is not null)
            animations.Add(ModAnimation.AaColor(_back, Border.BackgroundProperty, color, duration));
    }

    private void ApplyIconBrush(IBrush brush)
    {
        if (_path is not null)
        {
            _path.Fill = brush;
            _path.Stroke = brush;
        }
        if (_svgIcon is not null)
            _svgIcon.IconBrush = brush;
    }

    private void ClearBackImmediately()
    {
        if (_back is not null)
            _back.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
    }

    private Color GetForegroundColor() =>
        Foreground is SolidColorBrush customBrush ? customBrush.Color : Color.FromRgb(128, 128, 128);

    private Color FindColor(string key, Color fallback)
    {
        return LegacyResourceResolver.Color(this, key, fallback);
    }

    private static Color GetBlackThemeColor(byte alpha) =>
        AvaloniaThemeManager.IsDarkMode
            ? Color.FromArgb(alpha, 255, 255, 255)
            : Color.FromArgb(alpha, 0, 0, 0);

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    private static double GetScaleX(Control control) =>
        control.RenderTransform switch
        {
            ScaleTransform scale => scale.ScaleX,
            TransformGroup group => group.Children.OfType<ScaleTransform>().FirstOrDefault()?.ScaleX ?? 1d,
            _ => 1d
        };
}
