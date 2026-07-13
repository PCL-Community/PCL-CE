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

namespace PCL.Desktop.Controls.Legacy;

public sealed partial class MyRadioBox : Grid, IMyRadio
{
#pragma warning disable CA1711
    public delegate void PreviewChangeEventHandler(object sender, RouteEventArgs e);

    public delegate void PreviewCheckEventHandler(object sender, RouteEventArgs e);
#pragma warning restore CA1711

    private const double BorderUncheckedSize = 18d;
    // Keep both ellipses on whole device-independent pixels. The previous
    // 9 px dot required a 5.5 px offset and looked off-centre after rasterizing.
    private const double DotCheckedSize = 8d;
    private const int CheckAnimationMilliseconds = 150;
    private const int MouseInAnimationMilliseconds = 100;
    private const int MouseOutAnimationMilliseconds = 200;

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyRadioBox, bool>(nameof(Checked));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyRadioBox, string>(nameof(Text), string.Empty);

    private readonly TextBlock? _label;
    private readonly Ellipse? _border;
    private readonly Ellipse? _dot;
    private bool _isUpdatingGroup;
    private bool _mouseDowned;

    public MyRadioBox()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _border = this.FindControl<Ellipse>("ShapeBorder");
        _dot = this.FindControl<Ellipse>("ShapeDot");

        PointerEntered += (_, _) => RadioboxMouseEnterAnimation();
        PointerExited += (_, _) =>
        {
            RadioboxMouseLeave();
            RadioboxMouseLeaveAnimation();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
        AttachedToVisualTree += (_, _) => SyncVisual(animate: false);
        DetachedFromVisualTree += (_, _) => StopAnimation();
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RadioboxIsEnabledChanged());

        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(CheckedProperty).Subscribe(_ => SyncVisual(animate: true));
    }

    public event PreviewCheckEventHandler? PreviewCheck;

    public event PreviewChangeEventHandler? PreviewChange;

    public event IMyRadio.CheckEventHandler? Check;

    public event IMyRadio.ChangedEventHandler? Changed;

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, user: false);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public int Uuid { get; } = Random.Shared.Next();

    public InlineCollection Inlines =>
        _label?.Inlines ?? throw new InvalidOperationException("MyRadioBox text block is not initialized.");

    public void SetChecked(bool value, bool user)
    {
        if (_isUpdatingGroup)
        {
            SetCurrentValue(CheckedProperty, value);
            return;
        }

        if (value && user)
        {
            RouteEventArgs previewCheck = new(user);
            PreviewCheck?.Invoke(this, previewCheck);
            if (previewCheck.Handled)
                return;
        }

        bool wasChecked = Checked;
        if (wasChecked == value)
            return;

        RouteEventArgs previewChange = new(user);
        PreviewChange?.Invoke(this, previewChange);
        if (previewChange.Handled)
            return;

        SetCurrentValue(CheckedProperty, value);
        EnsureSingleCheckedInParent();

        RouteEventArgs changed = new(user);
        if (Checked)
            Check?.Invoke(this, changed);
        Changed?.Invoke(this, changed);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _mouseDowned = true;
        Focus();
        if (_border is not null)
        {
            ModAnimation.AniStart(
                ModAnimation.AaColor(_border, Shape.FillProperty, "ColorBrushBg1", MouseInAnimationMilliseconds),
                "MyRadioBox Background " + Uuid);
            if (!Checked)
            {
                ModAnimation.AniStart(
                    ModAnimation.AaScale(
                        _border,
                        16.5d - GetWidth(_border),
                        1000,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong),
                        absolute: true),
                    "MyRadioBox Border " + Uuid);
            }
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsEnabled || e.InitialPressMouseButton != MouseButton.Left)
            return;

        if (!_mouseDowned)
            return;

        SetChecked(true, user: true);
        _mouseDowned = false;
        if (_border is not null)
        {
            ModAnimation.AniStart(
                ModAnimation.AaColor(_border, Shape.FillProperty, "ColorBrushHalfWhite", MouseInAnimationMilliseconds),
                "MyRadioBox Background " + Uuid);
        }
        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEnabled || (e.Key != Key.Enter && e.Key != Key.Space))
            return;

        SetChecked(true, user: true);
        e.Handled = true;
    }

    private void EnsureSingleCheckedInParent()
    {
        if (Parent is not Panel panel)
            return;

        List<MyRadioBox> siblings = [];
        foreach (Control child in panel.Children)
        {
            if (child is MyRadioBox radio)
                siblings.Add(radio);
        }

        if (siblings.Count == 0)
            return;

        int checkedCount = siblings.Count(static radio => radio.Checked);
        if (checkedCount == 0)
        {
            siblings[0].SetCurrentValue(CheckedProperty, true);
            return;
        }

        if (checkedCount <= 1)
            return;

        _isUpdatingGroup = true;
        try
        {
            bool foundSelected = false;
            foreach (MyRadioBox radio in siblings)
            {
                bool keep = ReferenceEquals(radio, this) && Checked;
                if (!keep && radio.Checked && !foundSelected && !Checked)
                {
                    keep = true;
                    foundSelected = true;
                }

                if (radio.Checked != keep)
                    radio.SetCurrentValue(CheckedProperty, keep);
            }
        }
        finally
        {
            _isUpdatingGroup = false;
        }
    }

    private void SyncVisual(bool animate)
    {
        StopAnimation();

        if (_border is null || _dot is null)
            return;

        if (!animate)
        {
            _border.Width = BorderUncheckedSize;
            _border.Height = BorderUncheckedSize;
            if (Checked)
            {
                _dot.Width = DotCheckedSize;
                _dot.Height = DotCheckedSize;
                _dot.Margin = new Thickness(6d, 0d, 0d, 0d);
                _dot.Opacity = 1d;
                _border.Stroke = ResolveBrush(IsEnabled ? "ColorBrush2" : "ColorBrushGray4", IsEnabled ? "#0b5bcb" : "#a6a6a6");
                SyncDotBrush();
            }
            else
            {
                _dot.Width = 0d;
                _dot.Height = 0d;
                _dot.Margin = new Thickness(10d, 0d, 0d, 0d);
                _dot.Opacity = 0d;
                _border.Stroke = ResolveBrush(IsEnabled ? "ColorBrush1" : "ColorBrushGray4", IsEnabled ? "#343d4a" : "#a6a6a6");
                SyncDotBrush();
            }

            return;
        }

        if (Checked)
        {
            _dot.Margin = new Thickness(10d - GetWidth(_dot) / 2d, 0d, 0d, 0d);
            if (_dot.Opacity < 0.01d)
                _dot.Opacity = 1d;
            ModAnimation.AniStart(
                new List<ModAnimation.AniData>
                {
                    ModAnimation.AaScale(
                        _border,
                        10d - GetWidth(_border),
                        CheckAnimationMilliseconds,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak),
                        absolute: true),
                    ModAnimation.AaScale(
                        _border,
                        8d,
                        CheckAnimationMilliseconds * 2,
                        (int)Math.Round(CheckAnimationMilliseconds * 0.6d),
                        new ModAnimation.AniEaseOutBack(),
                        absolute: true)
                },
                "MyRadioBox Border " + Uuid);
            ModAnimation.AniStart(
                new List<ModAnimation.AniData>
                {
                    ModAnimation.AaScale(
                        _dot,
                        DotCheckedSize - GetWidth(_dot),
                        (int)Math.Round(CheckAnimationMilliseconds * 2.6d),
                        ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak),
                        absolute: true),
                    ModAnimation.AaOpacity(
                        _dot,
                        1d - _dot.Opacity,
                        (int)Math.Round(CheckAnimationMilliseconds * 0.5d),
                        (int)Math.Round(CheckAnimationMilliseconds * 0.6d))
                },
                "MyRadioBox Dot " + Uuid);
            AnimateSelectionBrush(
                IsPointerOver ? "ColorBrush3" : IsEnabled ? "ColorBrush2" : "ColorBrushGray4",
                CheckAnimationMilliseconds);
        }
        else
        {
            ModAnimation.AniStart(
                ModAnimation.AaScale(
                    _border,
                    BorderUncheckedSize - GetWidth(_border),
                    CheckAnimationMilliseconds,
                    ease: new ModAnimation.AniEaseOutFluent(),
                    absolute: true),
                "MyRadioBox Border " + Uuid);
            ModAnimation.AniStart(
                new List<ModAnimation.AniData>
                {
                    ModAnimation.AaScale(
                        _dot,
                        -GetWidth(_dot),
                        CheckAnimationMilliseconds,
                        ease: new ModAnimation.AniEaseInFluent(),
                        absolute: true),
                    ModAnimation.AaOpacity(
                        _dot,
                        -_dot.Opacity,
                        (int)Math.Round(CheckAnimationMilliseconds * 0.5d),
                        (int)Math.Round(CheckAnimationMilliseconds * 0.2d))
                },
                "MyRadioBox Dot " + Uuid);
            AnimateSelectionBrush(
                IsPointerOver ? "ColorBrush3" : IsEnabled ? "ColorBrush1" : "ColorBrushGray4",
                CheckAnimationMilliseconds);
        }
    }

    private void RadioboxMouseLeave()
    {
        if (!_mouseDowned || _border is null)
            return;

        _mouseDowned = false;
        ModAnimation.AniStart(
            ModAnimation.AaColor(_border, Shape.FillProperty, "ColorBrushHalfWhite", MouseInAnimationMilliseconds),
            "MyRadioBox Background " + Uuid);
        if (!Checked)
        {
            ModAnimation.AniStart(
                ModAnimation.AaScale(
                    _border,
                    BorderUncheckedSize - GetWidth(_border),
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong),
                    absolute: true),
                "MyRadioBox Border " + Uuid);
        }
    }

    private void RadioboxIsEnabledChanged()
    {
        if (_border is null || _label is null)
            return;

        if (IsEnabled)
        {
            RadioboxMouseLeaveAnimation();
            return;
        }

        AnimateSelectionBrush("ColorBrushGray4", MouseOutAnimationMilliseconds);
        ModAnimation.AniStart(
            ModAnimation.AaColor(_label, TextBlock.ForegroundProperty, "ColorBrushGray4", MouseOutAnimationMilliseconds),
            "MyRadioBox TextColor " + Uuid);
    }

    private void RadioboxMouseEnterAnimation()
    {
        if (_border is null || _label is null)
            return;

        AnimateSelectionBrush("ColorBrush3", MouseInAnimationMilliseconds);
        ModAnimation.AniStart(
            ModAnimation.AaColor(_label, TextBlock.ForegroundProperty, "ColorBrush3", MouseInAnimationMilliseconds),
            "MyRadioBox TextColor " + Uuid);
    }

    private void RadioboxMouseLeaveAnimation()
    {
        if (!IsEnabled || _border is null || _label is null)
            return;

        AnimateSelectionBrush(Checked ? "ColorBrush2" : "ColorBrush1", MouseOutAnimationMilliseconds);
        ModAnimation.AniStart(
            ModAnimation.AaColor(_label, TextBlock.ForegroundProperty, "ColorBrush1", MouseOutAnimationMilliseconds),
            "MyRadioBox TextColor " + Uuid);
    }

    private void StopAnimation()
    {
        ModAnimation.AniStop("MyRadioBox Border " + Uuid);
        ModAnimation.AniStop("MyRadioBox Dot " + Uuid);
        ModAnimation.AniStop("MyRadioBox BorderColor " + Uuid);
        ModAnimation.AniStop("MyRadioBox TextColor " + Uuid);
    }

    private IBrush ResolveBrush(string resourceKey, string fallback)
    {
        return LegacyResourceResolver.Brush(this, resourceKey, fallback);
    }

    private void AnimateSelectionBrush(string resourceKey, int duration)
    {
        List<ModAnimation.AniData> animations = [];
        if (_border is not null)
            animations.Add(ModAnimation.AaColor(_border, Shape.StrokeProperty, resourceKey, duration));
        if (_dot is not null)
            animations.Add(ModAnimation.AaColor(_dot, Shape.FillProperty, resourceKey, duration));

        if (animations.Count > 0)
            ModAnimation.AniStart(animations, "MyRadioBox BorderColor " + Uuid);
    }

    private void SyncDotBrush()
    {
        if (_border?.Stroke is not { } brush || _dot is null)
            return;

        _dot.Fill = brush;
    }

    private static double GetWidth(Control control) =>
        !double.IsNaN(control.Width) ? control.Width : Math.Max(0d, control.Bounds.Width);
}
