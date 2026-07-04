// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PCL.Desktop.Theme;

namespace PCL.Desktop.Controls.Legacy;

public enum MyHintTheme
{
    Red,
    Yellow,
    Blue
}

#pragma warning disable CA1708
public partial class MyHint : Border
{
    public enum Themes
    {
        Blue,
        Red,
        Yellow
    }

    private const double LightToneL2 = 0.5d;
    private const double LightToneL7 = 0.94d;
    private const double DarkToneL2 = 0.75d;
    private const double DarkToneL7 = 0.225d;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyHint, string>(nameof(Text), string.Empty);

    public new static readonly StyledProperty<Themes> ThemeProperty =
        AvaloniaProperty.Register<MyHint, Themes>(nameof(Theme), Themes.Red);

    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<MyHint, bool>(nameof(CanClose));

    public static readonly StyledProperty<bool> IsWarnProperty =
        AvaloniaProperty.Register<MyHint, bool>("IsWarn", true);

    private readonly TextBlock? _label;
    private readonly MyIconButton? _closeButton;
    private bool _isMouseDown;

    static MyHint()
    {
        IsWarnProperty.Changed.AddClassHandler<MyHint>((hint, e) =>
        {
            if (e.NewValue is bool value)
                hint.Theme = value ? Themes.Red : Themes.Blue;
        });
    }

    public MyHint()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _closeButton = this.FindControl<MyIconButton>("BtnClose");
        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(ThemeProperty).Subscribe(_ => RefreshTheme());
        this.GetObservable(CanCloseProperty).Subscribe(value =>
        {
            if (_closeButton is not null)
                _closeButton.IsVisible = value;
        });
        PointerPressed += MyHint_PointerPressed;
        PointerReleased += MyHint_PointerReleased;
        PointerExited += (_, _) => _isMouseDown = false;
        DetachedFromVisualTree += (_, _) => ModAnimation.AniStop($"MyCard Dispose {GetHashCode()}");
        RefreshTheme();
    }

    public event EventHandler? Click;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public new Themes Theme
    {
        get => GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    [Obsolete("IsWarn 已过时。请换用 Theme 属性。")]
    public bool IsWarn
    {
        get => GetValue(IsWarnProperty);
        set => SetValue(IsWarnProperty, value);
    }

    [Obsolete("isWarn 已过时。请换用 Theme 属性。")]
    public bool isWarn
    {
        get => IsWarn;
        set => IsWarn = value;
    }

    public bool HasBorder
    {
        get => BorderThickness.Top > 0d;
        set => ApplyBorderPresence(value);
    }

    public string RelativeSetup { get; set; } = string.Empty;

    public InlineCollection Inlines =>
        _label?.Inlines ?? throw new InvalidOperationException("MyHint text block is not initialized.");

    private void BtnClose_Click(object? sender, EventArgs e) =>
        ModAnimation.AniDispose(this, removeFromChildren: false);

    private void RefreshTheme()
    {
        double hue = Theme switch
        {
            Themes.Yellow => 40d,
            Themes.Blue => 210d,
            _ => 355d
        };

        double toneL2 = AvaloniaThemeManager.IsDarkMode ? DarkToneL2 : LightToneL2;
        double toneL7 = AvaloniaThemeManager.IsDarkMode ? DarkToneL7 : LightToneL7;
        Color foreground = FromHsl(hue, 0.9d, toneL2);
        Color background = FromHsl(hue, 0.9d, toneL7);

        BorderBrush = new SolidColorBrush(foreground);
        Background = new SolidColorBrush(background);
        if (_label is not null)
            _label.Foreground = new SolidColorBrush(foreground);
        if (_closeButton is not null)
            _closeButton.Foreground = new SolidColorBrush(foreground);

        ApplyBorderPresence(HasBorder);
    }

    private void ApplyBorderPresence(bool hasBorder)
    {
        BorderThickness = hasBorder
            ? new Thickness(3d, 1d, 1d, 1d)
            : new Thickness(3d, 0d, 0d, 0d);
    }

    private void MyHint_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isMouseDown = true;
        e.Handled = true;
    }

    private void MyHint_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isMouseDown)
            return;

        _isMouseDown = false;
        Click?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        hue = ((hue % 360d) + 360d) % 360d / 360d;
        saturation = Math.Clamp(saturation, 0d, 1d);
        lightness = Math.Clamp(lightness, 0d, 1d);

        if (saturation <= 0d)
        {
            byte gray = ToByte(lightness);
            return Color.FromRgb(gray, gray, gray);
        }

        double q = lightness < 0.5d
            ? lightness * (1d + saturation)
            : lightness + saturation - lightness * saturation;
        double p = 2d * lightness - q;

        return Color.FromRgb(
            ToByte(HueToRgb(p, q, hue + 1d / 3d)),
            ToByte(HueToRgb(p, q, hue)),
            ToByte(HueToRgb(p, q, hue - 1d / 3d)));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0d)
            t += 1d;
        if (t > 1d)
            t -= 1d;
        if (t < 1d / 6d)
            return p + (q - p) * 6d * t;
        if (t < 1d / 2d)
            return q;
        if (t < 2d / 3d)
            return p + (q - p) * (2d / 3d - t) * 6d;
        return p;
    }

    private static byte ToByte(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0d, 1d) * 255d);
}
#pragma warning restore CA1708
