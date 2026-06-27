// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Desktop.Controls.Legacy;

public sealed partial class MyRadioBox : Grid
{
    private const double BorderUncheckedSize = 18d;
    private const double BorderCheckedSize = 18d;
    private const double DotCheckedSize = 9d;
    private const int CheckAnimationMilliseconds = 150;

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyRadioBox, bool>(nameof(Checked));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyRadioBox, string>(nameof(Text), string.Empty);

    private readonly TextBlock? _label;
    private readonly Ellipse? _border;
    private readonly Ellipse? _dot;
    private readonly Stopwatch _animationClock = new();
    private DispatcherTimer? _animationTimer;
    private bool _isUpdatingGroup;

    public MyRadioBox()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _border = this.FindControl<Ellipse>("ShapeBorder");
        _dot = this.FindControl<Ellipse>("ShapeDot");

        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
        AttachedToVisualTree += (_, _) => SyncVisual(animate: false);
        DetachedFromVisualTree += (_, _) => StopAnimation();
        this.GetObservable(IsEnabledProperty).Subscribe(_ => SyncVisual(animate: true));

        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(CheckedProperty).Subscribe(_ => SyncVisual(animate: true));
    }

    public event EventHandler<RadioBoxChangingEventArgs>? PreviewCheck;

    public event EventHandler<RadioBoxChangingEventArgs>? PreviewChange;

    public event EventHandler<RadioBoxChangedEventArgs>? Check;

    public event EventHandler<RadioBoxChangedEventArgs>? Changed;

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

    public void SetChecked(bool value, bool user)
    {
        if (_isUpdatingGroup)
        {
            SetCurrentValue(CheckedProperty, value);
            return;
        }

        if (value && user)
        {
            RadioBoxChangingEventArgs previewCheck = new(user);
            PreviewCheck?.Invoke(this, previewCheck);
            if (previewCheck.Handled)
                return;
        }

        bool wasChecked = Checked;
        if (wasChecked == value)
            return;

        RadioBoxChangingEventArgs previewChange = new(user);
        PreviewChange?.Invoke(this, previewChange);
        if (previewChange.Handled)
            return;

        SetCurrentValue(CheckedProperty, value);
        EnsureSingleCheckedInParent();

        RadioBoxChangedEventArgs changed = new(user);
        if (Checked)
            Check?.Invoke(this, changed);
        Changed?.Invoke(this, changed);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsEnabled || e.InitialPressMouseButton != MouseButton.Left)
            return;

        SetChecked(true, user: true);
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

        IBrush stroke = ResolveBrush(Checked, IsEnabled);
        _border.Stroke = stroke;
        _dot.Fill = stroke;

        double targetDotSize = Checked ? DotCheckedSize : 0d;
        if (!animate)
        {
            _border.Width = BorderCheckedSize;
            _border.Height = BorderCheckedSize;
            _dot.Width = targetDotSize;
            _dot.Height = targetDotSize;
            _dot.Opacity = Checked ? 1d : 0d;
            return;
        }

        double startDotSize = _dot.Width;
        double startOpacity = _dot.Opacity;
        _animationClock.Restart();
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += (_, _) =>
        {
            double progress = Math.Clamp(
                _animationClock.Elapsed.TotalMilliseconds / CheckAnimationMilliseconds,
                0d,
                1d);
            double eased = EaseOutCubic(progress);
            double dotSize = Lerp(startDotSize, targetDotSize, eased);
            _border.Width = BorderUncheckedSize;
            _border.Height = BorderUncheckedSize;
            _dot.Width = dotSize;
            _dot.Height = dotSize;
            _dot.Opacity = Lerp(startOpacity, Checked ? 1d : 0d, eased);
            if (progress < 1d)
                return;

            StopAnimation();
        };
        _animationTimer.Start();
    }

    private static SolidColorBrush ResolveBrush(bool isChecked, bool isEnabled)
    {
        if (!isEnabled)
            return new SolidColorBrush(Color.Parse("#8c8c8c"));

        return new SolidColorBrush(Color.Parse(isChecked ? "#1370f3" : "#333333"));
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
    }

    private static double Lerp(double from, double to, double progress) =>
        from + (to - from) * progress;

    private static double EaseOutCubic(double progress)
    {
        double inverse = 1d - progress;
        return 1d - inverse * inverse * inverse;
    }
}

public sealed class RadioBoxChangingEventArgs(bool user) : EventArgs
{
    public bool User { get; } = user;

    public bool Handled { get; set; }
}

public sealed class RadioBoxChangedEventArgs(bool user) : EventArgs
{
    public bool User { get; } = user;
}
