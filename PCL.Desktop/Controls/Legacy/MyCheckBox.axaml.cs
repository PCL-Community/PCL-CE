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

public partial class MyCheckBox : Grid
{
#pragma warning disable CA1711
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, RouteEventArgs e);
#pragma warning restore CA1711

    private const int CheckAnimationMilliseconds = 150;
    private const int MouseInAnimationMilliseconds = 100;
    private const int MouseOutAnimationMilliseconds = 200;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyCheckBox, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<bool?> CheckedProperty =
        AvaloniaProperty.Register<MyCheckBox, bool?>(nameof(Checked), false);

    public static readonly StyledProperty<bool> IsThreeStateProperty =
        AvaloniaProperty.Register<MyCheckBox, bool>(nameof(IsThreeState));

    private readonly TextBlock? _label;
    private readonly Border? _border;
    private readonly PathShape? _check;
    private readonly Border? _indeterminate;
    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private bool? _lastSyncedState = false;
    private bool? _previousState = false;
    private bool _allowMouseDown = true;
    private bool _isLoaded;
    private bool _isUpdatingChecked;
    private bool _mouseDowned;

    public MyCheckBox()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _border = this.FindControl<Border>("ShapeBorder");
        _check = this.FindControl<PathShape>("ShapeCheck");
        _indeterminate = this.FindControl<Border>("ShapeIndeterminate");

        PointerPressed += CheckboxPointerPressed;
        PointerReleased += CheckboxPointerReleased;
        PointerEntered += (_, _) => CheckboxMouseEnterAnimation();
        PointerExited += (_, _) =>
        {
            CheckboxMouseLeave();
            CheckboxMouseLeaveAnimation();
        };
        AttachedToVisualTree += (_, _) =>
        {
            _isLoaded = true;
            _lastSyncedState = GetFinalState(Checked, IsThreeState);
            SyncUI(animate: false);
        };
        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(CheckedProperty).Subscribe(value =>
        {
            if (_isUpdatingChecked)
                return;

            bool? final = GetFinalState(value, IsThreeState);
            _previousState = _lastSyncedState;
            _lastSyncedState = final;
            SyncUI(_isLoaded);
        });
        this.GetObservable(IsEnabledProperty).Subscribe(_ => CheckboxIsEnabledChanged());
        SyncUI(animate: false);
    }

    public event ChangeEventHandler? Change;

    public event PreviewChangeEventHandler? PreviewChange;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public InlineCollection Inlines =>
        _label?.Inlines ?? throw new InvalidOperationException("MyCheckBox text block is not initialized.");

    public bool? Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, user: false);
    }

    public bool IsThreeState
    {
        get => GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    public void SetChecked(bool? value, bool user)
    {
        if (Checked.HasValue && value.HasValue && Checked.Value == value.Value)
            return;

        if (value == true && user)
        {
            RouteEventArgs preview = new(user);
            PreviewChange?.Invoke(this, preview);
            if (preview.Handled)
            {
                _mouseDowned = true;
                CheckboxMouseLeave();
                _mouseDowned = false;
                return;
            }
        }

        bool? final = GetFinalState(value, IsThreeState);
        _previousState = _lastSyncedState;
        _isUpdatingChecked = true;
        SetValue(CheckedProperty, final);
        _isUpdatingChecked = false;
        _lastSyncedState = final;

        if (_isLoaded)
            Change?.Invoke(this, user);
        SyncUI(_isLoaded);
    }

    private void CheckboxPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_mouseDowned)
            return;

        _mouseDowned = false;
        if (IsThreeState)
        {
            SetChecked(Checked switch
            {
                true => null,
                false => true,
                _ => false
            }, user: true);
        }
        else
        {
            SetChecked(Checked != true, user: true);
        }

        if (_border is not null)
            ModAnimation.AniStart(
                ModAnimation.AaColor(_border, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
                $"MyCheckBox Background {_uuid}");
        e.Handled = true;
    }

    private void CheckboxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_allowMouseDown || !IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _mouseDowned = true;
        Focus();
        if (_border is null)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaColor(_border, Border.BackgroundProperty, "ColorBrushBg1", 100),
            $"MyCheckBox Background {_uuid}");
        List<ModAnimation.AniData> animations =
        [
            ModAnimation.AaScale(
                _border,
                16.5d - _border.Width,
                1000,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong),
                absolute: true)
        ];
        if (Checked == true && _check is not null)
        {
            animations.Add(ModAnimation.AaScaleTransform(
                _check,
                0.9d - GetScaleX(_check),
                1000,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)));
        }

        ModAnimation.AniStart(animations, $"MyCheckBox Scale {_uuid}");
        e.Handled = true;
    }

    private void CheckboxMouseLeave()
    {
        if (!_mouseDowned)
            return;

        _mouseDowned = false;
        if (_border is null)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaColor(_border, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
            $"MyCheckBox Background {_uuid}");
        List<ModAnimation.AniData> animations =
        [
            ModAnimation.AaScale(
                _border,
                18d - _border.Width,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong),
                absolute: true)
        ];
        if (Checked == true && _check is not null)
        {
            animations.Add(ModAnimation.AaScaleTransform(
                _check,
                1d - GetScaleX(_check),
                500,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)));
        }

        ModAnimation.AniStart(animations, $"MyCheckBox Scale {_uuid}");
    }

    private void CheckboxIsEnabledChanged()
    {
        if (_label is null || _border is null)
            return;

        if (_isLoaded)
        {
            if (IsEnabled)
            {
                CheckboxMouseLeaveAnimation();
            }
            else
            {
                AnimateSelectionBrush("ColorBrushGray4", MouseOutAnimationMilliseconds);
                ModAnimation.AniStart(
                    ModAnimation.AaColor(_label, TextBlock.ForegroundProperty, "ColorBrushGray4", MouseOutAnimationMilliseconds),
                    $"MyCheckBox TextColor {_uuid}");
            }
            return;
        }

        ModAnimation.AniStop($"MyCheckBox TextColor {_uuid}");
        ModAnimation.AniStop($"MyCheckBox BorderColor {_uuid}");
        _label.Foreground = FindBrush(IsEnabled ? "ColorBrush1" : "ColorBrushGray4", IsEnabled ? "#343d4a" : "#a6a6a6");
        _border.BorderBrush = FindBrush(
            IsEnabled ? Checked == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4",
            IsEnabled ? Checked == true ? "#0b5bcb" : "#343d4a" : "#a6a6a6");
        SyncSelectionBrush();
    }

    private void CheckboxMouseEnterAnimation()
    {
        if (_label is null || _border is null || !IsEnabled)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaColor(_label, TextBlock.ForegroundProperty, "ColorBrush3", MouseInAnimationMilliseconds),
            $"MyCheckBox TextColor {_uuid}");
        AnimateSelectionBrush("ColorBrush3", MouseInAnimationMilliseconds);
    }

    private void CheckboxMouseLeaveAnimation()
    {
        if (_label is null || _border is null || !IsEnabled)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaColor(_label, TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4", MouseOutAnimationMilliseconds),
            $"MyCheckBox TextColor {_uuid}");
        AnimateSelectionBrush(
            IsEnabled ? Checked == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4",
            MouseOutAnimationMilliseconds);
    }

    private void SyncUI(bool animate)
    {
        bool? final = GetFinalState(Checked, IsThreeState);
        if (!animate)
        {
            StopStateAnimations();
            SetScale(_check, final == true ? 1d : 0d);
            SetScale(_indeterminate, final is null ? 1d : 0d);
            if (_border is not null)
            {
                _border.Width = 18d;
                _border.Height = 18d;
                _border.Margin = new Thickness(1d, 0d, 0d, 0d);
                _border.BorderBrush = FindBrush(
                    IsEnabled ? final == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4",
                    IsEnabled ? final == true ? "#0b5bcb" : "#343d4a" : "#a6a6a6");
                _border.Background = FindBrush("ColorBrushHalfWhite", "#55ffffff");
                SyncSelectionBrush();
            }
            if (_label is not null)
                _label.Foreground = FindBrush(IsEnabled ? "ColorBrush1" : "ColorBrushGray4", IsEnabled ? "#343d4a" : "#a6a6a6");
            return;
        }

        _allowMouseDown = false;
        switch (final, _previousState)
        {
            case (true, null):
                AniBackgroundScale();
                AniIndeterminateHide();
                AniCheckShow();
                AniColorChecked();
                AniAllowMouseDown();
                break;
            case (true, false):
                AniBackgroundScale();
                AniCheckShow();
                AniColorChecked();
                AniAllowMouseDown();
                break;
            case (false, true):
                AniBackgroundScale();
                AniCheckHide();
                AniColorUnchecked();
                AniAllowMouseDown();
                break;
            case (false, null):
                AniBackgroundScale();
                AniIndeterminateHide();
                AniCheckHide();
                AniColorUnchecked();
                AniAllowMouseDown();
                break;
            case (null, true):
                AniBackgroundScale();
                AniCheckHide();
                AniIndeterminateShow();
                AniColorUnchecked();
                AniAllowMouseDown();
                break;
            case (null, false):
                AniBackgroundScale();
                AniIndeterminateShow();
                AniColorUnchecked();
                AniAllowMouseDown();
                break;
            default:
                _allowMouseDown = true;
                break;
        }
    }

    private void AniBackgroundScale()
    {
        if (_border is null)
            return;

        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaScale(
                _border,
                12d - _border.Width,
                CheckAnimationMilliseconds,
                ease: new ModAnimation.AniEaseOutFluent(),
                absolute: true),
            ModAnimation.AaScale(
                _border,
                6d,
                CheckAnimationMilliseconds * 2,
                (int)Math.Round(CheckAnimationMilliseconds * 0.7d),
                new ModAnimation.AniEaseOutBack(),
                absolute: true)
        }, $"MyCheckBox Background Scale {_uuid}");
    }

    private void AniCheckShow()
    {
        if (_check is null)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(
                _check,
                1d - GetScaleX(_check),
                CheckAnimationMilliseconds * 2,
                (int)Math.Round(CheckAnimationMilliseconds * 0.7d),
                new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            $"MyCheckBox Check Scale Show{_uuid}");
    }

    private void AniCheckHide()
    {
        if (_check is null)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(
                _check,
                -GetScaleX(_check),
                (int)Math.Round(CheckAnimationMilliseconds * 0.9d),
                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            $"MyCheckBox Check Scale Hide{_uuid}");
    }

    private void AniIndeterminateShow()
    {
        if (_indeterminate is null)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(
                _indeterminate,
                1d - GetScaleX(_indeterminate),
                CheckAnimationMilliseconds * 2,
                (int)Math.Round(CheckAnimationMilliseconds * 0.7d),
                new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            $"MyCheckBox Indeterminate Scale Show{_uuid}");
    }

    private void AniIndeterminateHide()
    {
        if (_indeterminate is null)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(
                _indeterminate,
                -GetScaleX(_indeterminate),
                (int)Math.Round(CheckAnimationMilliseconds * 0.9d),
                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            $"MyCheckBox Indeterminate Scale Hide{_uuid}");
    }

    private void AniAllowMouseDown() =>
        ModAnimation.AniStart(
            ModAnimation.AaCode(() => _allowMouseDown = true, CheckAnimationMilliseconds * 2),
            $"MyCheckBox AllowMouseDown {_uuid}");

    private void AniColorChecked()
    {
        if (_border is null)
            return;

        AnimateSelectionBrush(
            IsEnabled ? IsPointerOver ? "ColorBrush3" : "ColorBrush2" : "ColorBrushGray4",
            CheckAnimationMilliseconds);
    }

    private void AniColorUnchecked()
    {
        if (_border is null)
            return;

        AnimateSelectionBrush(
            IsEnabled ? IsPointerOver ? "ColorBrush3" : "ColorBrush1" : "ColorBrushGray4",
            CheckAnimationMilliseconds);
    }

    private void StopStateAnimations()
    {
        ModAnimation.AniStop($"MyCheckBox Background Scale {_uuid}");
        ModAnimation.AniStop($"MyCheckBox Check Scale Show{_uuid}");
        ModAnimation.AniStop($"MyCheckBox Check Scale Hide{_uuid}");
        ModAnimation.AniStop($"MyCheckBox Indeterminate Scale Show{_uuid}");
        ModAnimation.AniStop($"MyCheckBox Indeterminate Scale Hide{_uuid}");
        ModAnimation.AniStop($"MyCheckBox BorderColor {_uuid}");
        ModAnimation.AniStop($"MyCheckBox AllowMouseDown {_uuid}");
    }

    private static bool? GetFinalState(bool? value, bool isThreeState) =>
        isThreeState ? value : value == true;

    private static double GetScaleX(Control? control) =>
        control?.RenderTransform is ScaleTransform scale ? scale.ScaleX : 0d;

    private static void SetScale(Control? control, double scale)
    {
        if (control is null)
            return;

        ControlVisualHelpers.SetCenterScale(control, scale);
    }

    private void AnimateSelectionBrush(string resourceKey, int duration)
    {
        List<ModAnimation.AniData> animations = [];
        if (_border is not null)
            animations.Add(ModAnimation.AaColor(_border, Border.BorderBrushProperty, resourceKey, duration));
        if (_check is not null)
            animations.Add(ModAnimation.AaColor(_check, Shape.FillProperty, resourceKey, duration));
        if (_indeterminate is not null)
            animations.Add(ModAnimation.AaColor(_indeterminate, Border.BackgroundProperty, resourceKey, duration));

        if (animations.Count > 0)
            ModAnimation.AniStart(animations, $"MyCheckBox BorderColor {_uuid}");
    }

    private void SyncSelectionBrush()
    {
        if (_border?.BorderBrush is not { } brush)
            return;

        if (_check is not null)
            _check.Fill = brush;
        if (_indeterminate is not null)
            _indeterminate.Background = brush;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }
}
