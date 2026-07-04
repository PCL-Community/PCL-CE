// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public class MyTextButton : Button
{
    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 200;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyTextButton, string>(nameof(Text), string.Empty);

    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private string? _colorName;
    private bool _isMouseDown;

    protected override Type StyleKeyOverride => typeof(Button);

    public MyTextButton()
    {
        Background = Brushes.Transparent;
        BorderThickness = new Thickness();
        Padding = new Thickness();
        Cursor = new Cursor(StandardCursorType.Hand);
        Content = Text;

        PointerPressed += MyTextButtonPointerPressed;
        PointerExited += (_, _) => MyTextButtonPointerLeave();
        PointerReleased += MyTextButtonPointerReleased;
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        PointerPressed += (_, _) => RefreshColor();
        PointerReleased += (_, _) => RefreshColor();
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(TextProperty).Subscribe(AnimateText);
        RefreshColor();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void AnimateText(string text)
    {
        if (Equals(Content, text))
            return;

        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaOpacity(this, -Opacity, 50),
                ModAnimation.AaCode(() => Content = text, after: true),
                ModAnimation.AaOpacity(this, 1d, 170)
            },
            $"MyTextButton Text {_uuid}");
    }

    private (string ForeName, int Time) GetVisualState()
    {
        if (!IsEnabled)
            return ("ColorBrushGray4", AnimationTimeOut);
        if (_isMouseDown)
            return ("ColorBrush4", 30);
        if (IsPointerOver)
            return ("ColorBrush3", AnimationTimeIn);

        return ("ColorBrush1", AnimationTimeOut);
    }

    private void MyTextButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isMouseDown = true;
    }

    private void MyTextButtonPointerLeave()
    {
        _isMouseDown = false;
    }

    private void MyTextButtonPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isMouseDown)
            return;

        _isMouseDown = false;
    }

    private void RefreshColor()
    {
        var (foreName, time) = GetVisualState();
        if (_colorName == foreName)
            return;

        _colorName = foreName;
        Cursor = IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        ModAnimation.AniStart(
            ModAnimation.AaColor(this, ForegroundProperty, foreName, time),
            $"MyTextButton Color {_uuid}");
    }
}
