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
using Avalonia.Threading;

namespace PCL.Desktop.Controls.Legacy;

public partial class MySlider : Border
{
    // Keep the original WPF event signatures so copied XAML/code-behind can bind without adapters.
    #pragma warning disable CA1711, CA1708
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, SliderPreviewChangeEventArgs e);
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
    private readonly DispatcherTimer _keyPopupTimer;
    private IPointer? _capturedPointer;
    private bool _changeByKey;
    private bool _isDragging;
    private bool _isSyncingValueProperty;
    private int _value;

    public MySlider()
    {
        AvaloniaXamlLoader.Load(this);

        _mainPanel = this.FindControl<Grid>("PanMain");
        _lineBack = this.FindControl<Line>("LineBack");
        _lineFore = this.FindControl<Line>("LineFore");
        _shapeDot = this.FindControl<Ellipse>("ShapeDot");
        _popup = this.FindControl<Popup>("Popup");
        _textHint = this.FindControl<TextBlock>("TextHint");
        if (_popup is not null)
            _popup.PlacementTarget = _shapeDot;
        _keyPopupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _keyPopupTimer.Tick += (_, _) =>
        {
            _keyPopupTimer.Stop();
            if (_popup is not null)
                _popup.IsOpen = false;
        };

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

    public event ChangeEventHandler? Change;

    public event PreviewChangeEventHandler? PreviewChange;

    public Delegate? getHintText { get; set; }

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
        SetDotScale(1d);
        RefreshColor();
        if (_popup is not null)
            _popup.IsOpen = false;
    }

    public void RefreshPopup()
    {
        if (getHintText is null || _popup is null || _textHint is null)
            return;

        _textHint.Text = getHintText.DynamicInvoke(Value)?.ToString() ?? string.Empty;
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

        SliderPreviewChangeEventArgs preview = new();
        PreviewChange?.Invoke(this, preview);
        if (preview.Handled)
        {
            _value = oldValue;
            SyncValueProperty(oldValue);
            DragStop();
            RefreshWidth(null, null);
            return;
        }

        RefreshWidth(null, null);
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

        if (_lineBack is null || _lineFore is null || _shapeDot is null)
            return;

        double trackWidth = GetTrackWidth();
        double newWidth = MaxValue <= 0 ? 0d : _value / (double)MaxValue * trackWidth;
        _lineFore.Width = Math.Max(0d, newWidth + (newWidth < 0.5d ? 0d : 0.5d));
        _lineBack.Width = Math.Max(0d, trackWidth - newWidth + (trackWidth - newWidth < 0.5d ? 0d : 0.5d));
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
        SetDotScale(1.3d);
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

        IBrush brush = IsEnabled
            ? (IsPointerOver || _isDragging
                ? FindBrush("ColorBrush3", "#1370f3")
                : FindBrush("ColorBrushBg0", "#96c0f9"))
            : FindBrush("ColorBrushGray5", "#cccccc");

        BorderBrush = brush;
        if (_lineFore is not null)
            _lineFore.Stroke = brush;
        _shapeDot.Stroke = brush;
        _shapeDot.Fill = brush;
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
            _keyPopupTimer.Stop();
            _keyPopupTimer.Interval = TimeSpan.FromMilliseconds(_changeByKey ? 800 : 700);
            _keyPopupTimer.Start();
        }
    }

    private double GetTrackWidth() =>
        Math.Max(0d, Bounds.Width - (_shapeDot?.Width ?? 0d));

    private Control GetPointerReference() => _mainPanel is not null ? _mainPanel : this;

    private void SetDotScale(double scale)
    {
        if (_shapeDot is null)
            return;

        if (_shapeDot.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform();
            _shapeDot.RenderTransform = transform;
        }

        transform.ScaleX = scale;
        transform.ScaleY = scale;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        if (this.TryGetResource(key, null, out object? resource) && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallback));
    }
}

#pragma warning disable CA1708
public sealed class SliderPreviewChangeEventArgs(bool raiseByMouse = false) : EventArgs
{
    public bool Handled { get; set; }

    public bool handled
    {
        get => Handled;
        set => Handled = value;
    }

    public bool RaiseByMouse { get; } = raiseByMouse;

    public bool raiseByMouse => RaiseByMouse;
}
#pragma warning restore CA1708
