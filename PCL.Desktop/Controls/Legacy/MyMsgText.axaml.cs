// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public sealed class MyMsgTextClosedEventArgs(int result) : EventArgs
{
    public int Result { get; } = result;

    public bool IsPrimary => Result == 1;
}

public partial class MyMsgText : Grid
{
    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private TranslateTransform? _transformPos;
    private RotateTransform? _transformRotate;
    private int _pendingResult = 1;
    private AnimationMode _animationMode;
    private Action? _button1Action;
    private Action? _button2Action;
    private Action? _button3Action;

    public MyMsgText()
    {
        AvaloniaXamlLoader.Load(this);
        CaptureTransforms();
        Opacity = 0d;
    }

    public event EventHandler<MyMsgTextClosedEventArgs>? Closed;

    public bool IsClosing => _animationMode == AnimationMode.Closing;

    public void Configure(
        string title,
        string caption,
        string primaryButton = "确定",
        string secondaryButton = "",
        string thirdButton = "",
        bool isWarn = false,
        Action? primaryAction = null,
        Action? secondaryAction = null,
        Action? thirdAction = null)
    {
        _button1Action = primaryAction;
        _button2Action = secondaryAction;
        _button3Action = thirdAction;

        if (this.FindControl<TextBlock>("LabTitle") is { } titleBlock)
        {
            titleBlock.Text = title;
            titleBlock.Foreground = isWarn
                ? FindBrush("ColorBrushRedLight", "#ff4c4c")
                : FindBrush("ColorBrush2", "#3a3a3a");
        }
        if (this.FindControl<TextBlock>("LabCaption") is { } captionBlock)
            captionBlock.Text = caption;

        ConfigureSecondaryButton(this.FindControl<MyButton>("Btn2"), secondaryButton);
        ConfigureSecondaryButton(this.FindControl<MyButton>("Btn3"), thirdButton);
        ConfigurePrimaryButton(primaryButton, isWarn);
    }

    public void BeginShowAnimation()
    {
        CaptureTransforms();
        _animationMode = AnimationMode.Opening;
        Opacity = 0d;
        SetTransform(40d, -4d);
        string name = $"MyMsgBox {_uuid}";
        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaOpacity(this, 1d, 120, 60),
            ModAnimation.AaDouble(AddTransformY, -GetTransformY(), 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaDouble(AddTransformAngle, -GetTransformAngle(), 300, 60, new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => _animationMode = AnimationMode.None, after: true)
        }, name);
    }

    public void BeginCloseAnimation(Action? completed = null)
    {
        if (_animationMode == AnimationMode.Closing)
            return;

        CaptureTransforms();
        _animationMode = AnimationMode.Closing;
        string name = $"MyMsgBox {_uuid}";
        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(AddTransformY, 20d - GetTransformY(), 150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(AddTransformAngle, 6d - GetTransformAngle(), 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() =>
            {
                _animationMode = AnimationMode.None;
                completed?.Invoke();
            }, after: true)
        }, name);
    }

    private void Btn1Click(object? sender, EventArgs e)
    {
        if (_animationMode == AnimationMode.Closing)
            return;

        if (_button1Action is not null)
        {
            _button1Action();
            return;
        }

        CloseWithResult(1);
    }

    private void Btn2Click(object? sender, EventArgs e)
    {
        if (_animationMode == AnimationMode.Closing)
            return;

        if (_button2Action is not null)
        {
            _button2Action();
            return;
        }

        CloseWithResult(2);
    }

    private void Btn3Click(object? sender, EventArgs e)
    {
        if (_animationMode == AnimationMode.Closing)
            return;

        if (_button3Action is not null)
        {
            _button3Action();
            return;
        }

        CloseWithResult(3);
    }

    private void ConfigurePrimaryButton(string text, bool isWarn)
    {
        if (this.FindControl<MyButton>("Btn1") is not { } primary)
            return;

        primary.Text = text;
        primary.ColorType = isWarn ? MyButton.ColorState.Red : MyButton.ColorState.Normal;
        bool hasSecondaryAction = this.FindControl<MyButton>("Btn2") is { IsVisible: true };
        if (hasSecondaryAction && !isWarn)
            primary.ColorType = MyButton.ColorState.Highlight;
    }

    private static void ConfigureSecondaryButton(MyButton? button, string text)
    {
        if (button is null)
            return;

        button.Text = text;
        button.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void CloseWithResult(int result)
    {
        if (_animationMode == AnimationMode.Closing)
            return;

        _pendingResult = result;
        BeginCloseAnimation(() => Closed?.Invoke(this, new MyMsgTextClosedEventArgs(_pendingResult)));
    }

    private void CaptureTransforms()
    {
        if (RenderTransform is not TransformGroup group)
            return;

        foreach (ITransform transform in group.Children)
        {
            _transformRotate ??= transform as RotateTransform;
            _transformPos ??= transform as TranslateTransform;
        }
    }

    private void SetTransform(double y, double angle)
    {
        if (_transformPos is not null)
            _transformPos.Y = y;
        if (_transformRotate is not null)
            _transformRotate.Angle = angle;
    }

    private double GetTransformY() =>
        _transformPos?.Y ?? 0d;

    private double GetTransformAngle() =>
        _transformRotate?.Angle ?? 0d;

    private void AddTransformY(double value)
    {
        if (_transformPos is not null)
            _transformPos.Y += value;
    }

    private void AddTransformAngle(double value)
    {
        if (_transformRotate is not null)
            _transformRotate.Angle += value;
    }

    private IBrush FindBrush(string resourceKey, string fallback)
    {
        return LegacyResourceResolver.Brush(this, resourceKey, fallback);
    }

    private enum AnimationMode
    {
        None,
        Opening,
        Closing
    }
}
