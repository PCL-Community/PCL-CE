// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public partial class MySlider : Border
{
    // Keep the original WPF event signatures so copied XAML/code-behind can bind without adapters.
    #pragma warning disable CA1711, CA1708
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, RouteEventArgs e);
    #pragma warning restore CA1711, CA1708

    public static readonly StyledProperty<int> MaxValueProperty =
        AvaloniaProperty.Register<MySlider, int>(nameof(MaxValue), 100);

    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<MySlider, int>(nameof(Value));

    public static readonly StyledProperty<uint> ValueByKeyProperty =
        AvaloniaProperty.Register<MySlider, uint>(nameof(ValueByKey), 1U);

    private readonly Grid? _mainPanel;
    private readonly Line? _lineBack;
    private readonly Line? _lineFore;
    private readonly Ellipse? _shapeDot;
    private readonly Popup? _popup;
    private readonly TextBlock? _textHint;
    private IPointer? _capturedPointer;
    private bool _changeByKey;
    private bool _isDragging;
    private bool _isSyncingValueProperty;
    private int _value;

    public MySlider()
    {
        AvaloniaXamlLoader.Load(this);
        // Matches WPF MyScrollViewer #3854: focusing a slider must not auto-scroll the page.
        ScrollViewer.SetBringIntoViewOnFocusChange(this, false);

        _mainPanel = this.FindControl<Grid>("PanMain");
        _lineBack = this.FindControl<Line>("LineBack");
        _lineFore = this.FindControl<Line>("LineFore");
        _shapeDot = this.FindControl<Ellipse>("ShapeDot");
        _popup = this.FindControl<Popup>("Popup");
        _textHint = this.FindControl<TextBlock>("TextHint");
        if (_popup is not null)
            _popup.PlacementTarget = _shapeDot;

        SizeChanged += RefreshWidth;
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());
        PointerEntered += (_, _) =>
        {
            Focus();
            RefreshColor();
        };
        PointerExited += (_, _) => RefreshColor();
        PointerPressed += DragStart;
        PointerMoved += OnDragPointerMoved;
        PointerReleased += OnDragPointerReleased;
        KeyDown += MySlider_KeyDown;
        this.GetObservable(MaxValueProperty).Subscribe(_ => RefreshWidth(null, null));
        this.GetObservable(ValueProperty).Subscribe(value =>
        {
            if (!_isSyncingValueProperty && value != _value)
                SetSliderValue(value, user: false, syncStyledProperty: false);
        });
        RefreshColor();
    }

    public int Uuid { get; } = Random.Shared.Next();

    public event ChangeEventHandler? Change;

    public event PreviewChangeEventHandler? PreviewChange;

    public Func<int, object?>? getHintText { get; set; }

    public int MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, Math.Max(1, value));
    }

    public int Value
    {
        get => _value;
        set => SetSliderValue(value, user: false);
    }

    public uint ValueByKey
    {
        get => GetValue(ValueByKeyProperty);
        set => SetValue(ValueByKeyProperty, value);
    }

    public void DragDoing(Point pointerPosition)
    {
        if (_shapeDot is null || _mainPanel is null)
            return;

        double trackWidth = GetTrackWidth();
        if (trackWidth <= 0d)
            return;

        double percent = Math.Clamp((pointerPosition.X - _shapeDot.Width / 2d) / trackWidth, 0d, 1d);
        int newValue = (int)Math.Round(percent * MaxValue);
        if (newValue != Value)
            SetSliderValue(newValue, user: true);
        RefreshPopup();
    }

    public void DragStop()
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        _capturedPointer?.Capture(null);
        _capturedPointer = null;
        AnimateDotScale(1d, 200);
        RefreshColor();
        if (_popup is not null)
            _popup.IsOpen = false;
    }

    public void RefreshPopup()
    {
        if (getHintText is null || _popup is null || _textHint is null)
            return;

        _textHint.Text = getHintText(Value)?.ToString() ?? string.Empty;
        _popup.HorizontalOffset = _shapeDot?.Margin.Left ?? 0d;
        _popup.IsOpen = true;
    }

    private void SetSliderValue(int value, bool user, bool syncStyledProperty = true)
    {
        int newValue = (int)Math.Round(Math.Clamp(value, 0d, MaxValue));
        if (_value == newValue)
            return;

        int oldValue = _value;
        _value = newValue;
        if (syncStyledProperty)
            SyncValueProperty(newValue);

        if (ModAnimation.AniControlEnabled == 0)
        {
            RouteEventArgs preview = new(user);
            PreviewChange?.Invoke(this, preview);
            if (preview.Handled)
            {
                _value = oldValue;
                SyncValueProperty(oldValue);
                DragStop();
                RefreshProgress(animate: false);
                return;
            }
        }

        RefreshProgress(animate: true);
        if (ModAnimation.AniControlEnabled == 0)
            Change?.Invoke(this, false);
    }

    private void SyncValueProperty(int value)
    {
        _isSyncingValueProperty = true;
        try
        {
            SetCurrentValue(ValueProperty, value);
        }
        finally
        {
            _isSyncingValueProperty = false;
        }
    }

    private void RefreshWidth(object? sender, SizeChangedEventArgs? e)
    {
        if (_mainPanel is not null && e is not null)
            _mainPanel.Width = e.NewSize.Width;

        RefreshProgress(animate: false);
    }

    private void RefreshProgress(bool animate)
    {
        if (_lineBack is null || _lineFore is null || _shapeDot is null)
            return;

        double trackWidth = GetTrackWidth();
        double newWidth = MaxValue <= 0 ? 0d : _value / (double)MaxValue * trackWidth;
        double foreWidth = Math.Max(0d, newWidth + (newWidth < 0.5d ? 0d : 0.5d));
        double backWidth = Math.Max(0d, trackWidth - newWidth + (trackWidth - newWidth < 0.5d ? 0d : 0.5d));
        if (animate && ControlVisualHelpers.ShouldAnimate(this) && trackWidth > 0d)
        {
            double deltaProcess = Math.Abs(_lineFore.Width / trackWidth - _value / (double)MaxValue);
            double time = (1d - Math.Pow(1d - deltaProcess, 3d)) * 300d + (_changeByKey ? 100d : 0d);
            int duration = (int)Math.Round(time);
            ModAnimation.AniEase ease = duration > 50
                ? new ModAnimation.AniEaseOutFluent()
                : new ModAnimation.AniEaseLinear();
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaWidth(_lineFore, foreWidth - _lineFore.Width, duration, ease: ease),
                    ModAnimation.AaWidth(_lineBack, backWidth - _lineBack.Width, duration, ease: ease),
                    ModAnimation.AaX(_shapeDot, newWidth - _shapeDot.Margin.Left, duration, ease: ease)
                },
                "MySlider Progress " + Uuid);
            return;
        }

        ModAnimation.AniStop("MySlider Progress " + Uuid);
        _lineFore.Width = foreWidth;
        _lineBack.Width = backWidth;
        _shapeDot.Margin = new Thickness(newWidth, 0d, 0d, 0d);
    }

    private void DragStart(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isDragging = true;
        _capturedPointer = e.Pointer;
        e.Pointer.Capture(this);
        Focus();
        AnimateDotScale(1.3d, 40);
        RefreshColor();
        DragDoing(e.GetPosition(GetPointerReference()));
        e.Handled = true;
    }

    private void OnDragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
            return;

        DragDoing(e.GetPosition(GetPointerReference()));
        e.Handled = true;
    }

    private void OnDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
            return;

        DragDoing(e.GetPosition(GetPointerReference()));
        DragStop();
        e.Handled = true;
    }

    private void RefreshColor()
    {
        if (_shapeDot is null)
            return;

        string foregroundName;
        string dotFillName;
        int animationTime;
        if (IsEnabled)
        {
            if (_isDragging || IsPointerOver)
            {
                foregroundName = "ColorBrush3";
                dotFillName = "ColorBrush3";
                animationTime = 40;
            }
            else
            {
                foregroundName = "ColorBrushBg0";
                dotFillName = "ColorBrushBg0";
                animationTime = 100;
            }
        }
        else
        {
            foregroundName = "ColorBrushGray5";
            dotFillName = "ColorBrushGray5";
            animationTime = 200;
        }

        if (ControlVisualHelpers.ShouldAnimate(this))
        {
            List<ModAnimation.AniData> animations =
            [
                ModAnimation.AaColor(this, BorderBrushProperty, foregroundName, animationTime),
                ModAnimation.AaColor(_shapeDot, Shape.FillProperty, dotFillName, animationTime),
                ModAnimation.AaColor(_shapeDot, Shape.StrokeProperty, foregroundName, animationTime)
            ];
            if (_lineFore is not null)
                animations.Add(ModAnimation.AaColor(_lineFore, Shape.StrokeProperty, foregroundName, animationTime));
            ModAnimation.AniStart(animations, "MySlider Color " + Uuid);
            return;
        }

        ModAnimation.AniStop("MySlider Color " + Uuid);
        IBrush foreground = FindBrush(foregroundName, "#96c0f9");
        IBrush dotFill = FindBrush(dotFillName, "#96c0f9");
        BorderBrush = foreground;
        if (_lineFore is not null)
            _lineFore.Stroke = foreground;
        _shapeDot.Stroke = foreground;
        _shapeDot.Fill = dotFill;
    }

    private void MySlider_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_isDragging)
            return;

        if (e.Key == Key.Left)
        {
            _changeByKey = true;
            SetSliderValue(Value - (int)ValueByKey, user: true);
            _changeByKey = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            _changeByKey = true;
            SetSliderValue(Value + (int)ValueByKey, user: true);
            _changeByKey = false;
            e.Handled = true;
        }
        else
        {
            return;
        }

        if (getHintText is not null)
        {
            RefreshPopup();
            ModAnimation.AniStop("MySlider KeyPopup " + Uuid);
            if (_popup is not null)
            {
                ModAnimation.AniStart(
                    ModAnimation.AaCode(
                        () => _popup.IsOpen = false,
                        (int)Math.Round(700d * ModAnimation.aniSpeed)),
                    "MySlider KeyPopup " + Uuid);
            }
        }
    }

    private double GetTrackWidth() =>
        Math.Max(0d, Bounds.Width - (_shapeDot?.Width ?? 0d));

    private Control GetPointerReference() => _mainPanel is not null ? _mainPanel : this;

    private void AnimateDotScale(double targetScale, int duration)
    {
        if (_shapeDot is null)
            return;

        if (ControlVisualHelpers.ShouldAnimate(this))
        {
            ModAnimation.AniStart(
                ModAnimation.AaScaleTransform(
                    _shapeDot,
                    targetScale - GetScaleX(_shapeDot),
                    duration,
                    ease: new ModAnimation.AniEaseOutFluent()),
                "MySlider Scale " + Uuid);
            return;
        }

        ControlVisualHelpers.SetCenterScale(_shapeDot, targetScale);
    }

    private static double GetScaleX(Control control) =>
        control.RenderTransform is ScaleTransform scale ? scale.ScaleX : 1d;

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }
}
